using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Birko.AI;
using Birko.AI.Models;
using Birko.AI.Providers;
using Birko.AI.Tools;
using Birko.Security;
using DraCode.KoboldLair.Agents;
using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Server.Services;
using DraCode.KoboldLair.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace DraCode.KoboldLair.Tests.Endpoint;

/// <summary>
/// The docked Dragon follows the project the person opened (TASK-151, FEATURE-026 D12): <c>select_project</c> makes the
/// project's saved conversation the session's, replays it after <c>project_selected</c>, and a reconnect resumes it.
/// Only the caller's own projects can be selected. With no project, <c>select_home</c> follows the caller's own home
/// conversation (TASK-149), kept apart from every project's.
/// </summary>
public class DragonProjectSocketTests : IDisposable
{
    private const string Secret = "test-secret-key-at-least-32-characters-long-xyz";
    private const string UserA = "11111111-1111-1111-1111-111111111111";
    private const string UserB = "22222222-2222-2222-2222-222222222222";
    private const string OwnerScopes = "view_own,manage_projects,execute_agents";

    private readonly string _projectsPath = Path.Combine(Path.GetTempPath(), "kl-dragon-project", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { if (Directory.Exists(_projectsPath)) Directory.Delete(_projectsPath, recursive: true); } catch { }
    }

    private WebApplicationFactory<Program> CreateFactory()
    {
        var config = new Dictionary<string, string?>
        {
            ["Authentication:Jwt:Enabled"] = "true",
            ["Authentication:Jwt:Secret"] = Secret,
            ["Authentication:Jwt:Issuer"] = "KoboldLair",
            ["Authentication:Jwt:Audience"] = "KoboldLair",
            ["KoboldLair:Data:DefaultBackend"] = "SqLite",
            ["KoboldLair:ProjectsPath"] = _projectsPath,
            // A provider so a Dragon session can create its agents; nothing reachable answers it, and nothing here asks.
            ["KoboldLair:DefaultProvider"] = "test-ollama",
            ["KoboldLair:Providers:0:Name"] = "test-ollama",
            ["KoboldLair:Providers:0:Type"] = "ollama",
            ["KoboldLair:Providers:0:DefaultModel"] = "none",
            ["KoboldLair:Providers:0:RequiresApiKey"] = "false",
            ["KoboldLair:Providers:0:CompatibleAgents:0"] = "all",
            ["KoboldLair:Providers:0:Configuration:baseUrl"] = "http://127.0.0.1:9",
        };

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(config));
            builder.ConfigureTestServices(services => services.RemoveAll<IHostedService>());
        });
    }

    private static void SafeDispose(WebApplicationFactory<Program> factory)
    {
        try { factory.Dispose(); }
        catch (AggregateException) { }
        catch (OperationCanceledException) { }
    }

    private static string Token(WebApplicationFactory<Program> factory, string sub) =>
        factory.Services.GetRequiredService<ITokenProvider>().GenerateToken(new Dictionary<string, string>
        {
            ["sub"] = sub,
            ["scope"] = OwnerScopes
        }).Token;

    private static async Task<string> CreateProject(WebApplicationFactory<Program> factory, string sub, string name)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/projects")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", Token(factory, sub)) },
            Content = JsonContent.Create(new { name })
        };
        var response = await factory.CreateClient().SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetString()!;
    }

    /// <summary>Save a conversation for the project, where the session's history goes (the SQL history table).</summary>
    private static async Task SaveConversation(WebApplicationFactory<Program> factory, string projectId, params string[] turns)
    {
        var folder = factory.Services.GetRequiredService<ProjectService>().GetProject(projectId)!.Paths.Output;
        var messages = turns.Select((text, i) => i % 2 == 0
            ? new SessionMessage(Guid.NewGuid().ToString(), "user_message",
                new Dictionary<string, object> { ["role"] = "user", ["content"] = text }, DateTime.UtcNow.AddMinutes(i))
            : new SessionMessage(Guid.NewGuid().ToString(), "dragon_message",
                new Dictionary<string, object> { ["type"] = "dragon_message", ["message"] = text }, DateTime.UtcNow.AddMinutes(i)))
            .ToList();
        await factory.Services.GetRequiredService<SqlHistoryRepository>().SaveHistoryAsync(folder, messages);
    }

    private static List<SessionMessage> Turns(params string[] turns) => turns.Select((text, i) => i % 2 == 0
        ? new SessionMessage(Guid.NewGuid().ToString(), "user_message",
            new Dictionary<string, object> { ["role"] = "user", ["content"] = text }, DateTime.UtcNow.AddMinutes(i))
        : new SessionMessage(Guid.NewGuid().ToString(), "dragon_message",
            new Dictionary<string, object> { ["type"] = "dragon_message", ["message"] = text }, DateTime.UtcNow.AddMinutes(i)))
        .ToList();

    private static Task SaveHome(WebApplicationFactory<Program> factory, string sub, params string[] turns) =>
        factory.Services.GetRequiredService<SqlHistoryRepository>().SaveHomeHistoryAsync(sub, Turns(turns));

    private static async Task<List<string>> SavedTexts(Task<string?> load)
    {
        var json = await load;
        return json == null ? new List<string>() : JsonDocument.Parse(json).RootElement.EnumerateArray()
            .Select(m => m.GetProperty("data")).Select(Text).ToList();
    }

    private sealed class Client(WebSocket socket) : IDisposable
    {
        private readonly List<JsonElement> _seen = new();

        public static async Task<Client> ConnectAsync(WebApplicationFactory<Program> factory, string token, string? sessionId = null)
        {
            var query = $"token={token}" + (sessionId == null ? "" : $"&sessionId={sessionId}");
            var uri = new UriBuilder(factory.Server.BaseAddress) { Scheme = "ws", Path = "/dragon", Query = query }.Uri;
            return new Client(await factory.Server.CreateWebSocketClient().ConnectAsync(uri, CancellationToken.None));
        }

        public IReadOnlyList<JsonElement> Seen => _seen;

        public Task SendAsync(object message) => socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(message),
            WebSocketMessageType.Text, true, CancellationToken.None);

        /// <summary>Messages until one matches <paramref name="match"/>; that one, or null after <paramref name="seconds"/>.</summary>
        public async Task<JsonElement?> WaitForAsync(Func<JsonElement, bool> match, int seconds = 10)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
            var buffer = new byte[64 * 1024];
            try
            {
                while (true)
                {
                    using var ms = new MemoryStream();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await socket.ReceiveAsync(buffer, cts.Token);
                        if (result.MessageType == WebSocketMessageType.Close)
                            return null;
                        ms.Write(buffer, 0, result.Count);
                    } while (!result.EndOfMessage);

                    var message = JsonDocument.Parse(Encoding.UTF8.GetString(ms.ToArray())).RootElement.Clone();
                    _seen.Add(message);
                    if (match(message))
                        return message;
                }
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        /// <summary>Select the project and collect the replay that follows <c>project_selected</c>.</summary>
        public async Task<(JsonElement Selected, List<string> Replayed)> SelectAsync(string projectId, int expected)
        {
            await SendAsync(new { type = "select_project", projectId });
            var selected = await WaitForAsync(m => Str(m, "type") == "project_selected" && Str(m, "projectId") == projectId);
            selected.Should().NotBeNull("the server answers select_project for the caller's own project");
            var from = _seen.Count;
            for (var i = 0; i < expected; i++)
                (await WaitForAsync(m => Bool(m, "isReplay"))).Should().NotBeNull("each saved message is replayed");
            var replayed = _seen.Skip(from).Where(m => Bool(m, "isReplay")).Select(Text).ToList();
            return (selected!.Value, replayed);
        }

        /// <summary>Go home and collect the replay that follows <c>home_selected</c>.</summary>
        public async Task<(JsonElement Selected, List<string> Replayed)> SelectHomeAsync(int expected)
        {
            await SendAsync(new { type = "select_home" });
            var selected = await WaitForAsync(m => Str(m, "type") == "home_selected");
            selected.Should().NotBeNull("the server answers select_home");
            var from = _seen.Count;
            for (var i = 0; i < expected; i++)
                (await WaitForAsync(m => Bool(m, "isReplay"))).Should().NotBeNull("each saved message is replayed");
            var replayed = _seen.Skip(from).Where(m => Bool(m, "isReplay")).Select(Text).ToList();
            return (selected!.Value, replayed);
        }

        public void Dispose() => socket.Dispose();
    }

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool Bool(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    private static string Text(JsonElement e) => Str(e, "content") ?? Str(e, "message") ?? "";

    [Fact]
    public async Task Selecting_a_project_replays_its_conversation_and_switching_replays_the_other_ones()
    {
        var factory = CreateFactory();
        try
        {
            var shop = await CreateProject(factory, UserA, "Shop");
            var blog = await CreateProject(factory, UserA, "Blog");
            await SaveConversation(factory, shop, "Build a shop", "Which payment provider?", "Stripe", "Noted: Stripe.");
            await SaveConversation(factory, blog, "Build a blog", "Static or dynamic?");

            using var client = await Client.ConnectAsync(factory, Token(factory, UserA));

            var (selected, replayed) = await client.SelectAsync(shop, expected: 4);
            Str(selected, "projectName").Should().Be("Shop");
            selected.GetProperty("messageCount").GetInt32().Should().Be(4);
            replayed.Should().Equal("Build a shop", "Which payment provider?", "Stripe", "Noted: Stripe.");

            (selected, replayed) = await client.SelectAsync(blog, expected: 2);
            Str(selected, "projectName").Should().Be("Blog");
            replayed.Should().Equal("Build a blog", "Static or dynamic?");

            (_, replayed) = await client.SelectAsync(shop, expected: 4);
            replayed.Should().Equal("Build a shop", "Which payment provider?", "Stripe", "Noted: Stripe.");
        }
        finally { SafeDispose(factory); }
    }

    [Fact]
    public async Task Client_ready_after_selecting_a_project_asks_for_decisions_and_does_not_wake_Dragon()
    {
        // TASK-160: the web dock sends client_ready after project_selected for its decision cards. With the welcome
        // already settled by the selected conversation, it once fell through to the chat and queued an empty message.
        var factory = CreateFactory();
        try
        {
            var shop = await CreateProject(factory, UserA, "Shop");
            await SaveConversation(factory, shop, "Build a shop", "Which payment provider?");
            using var client = await Client.ConnectAsync(factory, Token(factory, UserA));
            await client.SelectAsync(shop, expected: 2);

            await client.SendAsync(new { type = "client_ready" });

            var woke = await client.WaitForAsync(m => Str(m, "type") is "dragon_typing" or "dragon_thinking" or "dragon_message" or "error"
                || (Str(m, "type") == "user_message" && !Bool(m, "isReplay")), seconds: 3);
            woke.Should().BeNull("client_ready is not a message to Dragon");
            var folder = factory.Services.GetRequiredService<ProjectService>().GetProject(shop)!.Paths.Output;
            var saved = await factory.Services.GetRequiredService<SqlHistoryRepository>().LoadHistoryJsonAsync(folder);
            JsonDocument.Parse(saved!).RootElement.GetArrayLength().Should().Be(2, "nothing was added to the conversation");
        }
        finally { SafeDispose(factory); }
    }

    [Fact]
    public async Task Another_users_project_cannot_be_selected()
    {
        var factory = CreateFactory();
        try
        {
            var theirs = await CreateProject(factory, UserB, "Secret");
            await SaveConversation(factory, theirs, "B's private plan", "Understood.");

            using var client = await Client.ConnectAsync(factory, Token(factory, UserA));
            await client.SendAsync(new { type = "select_project", projectId = theirs });

            var error = await client.WaitForAsync(m => Str(m, "type") == "error" || Str(m, "type") == "project_selected");
            error.Should().NotBeNull();
            Str(error!.Value, "type").Should().Be("error");
            Str(error.Value, "errorType").Should().Be("project_not_found");
            client.Seen.Select(Text).Should().NotContain("B's private plan");
        }
        finally { SafeDispose(factory); }
    }

    [Fact]
    public async Task A_reconnect_resumes_the_session_with_the_selected_projects_conversation()
    {
        var factory = CreateFactory();
        try
        {
            var shop = await CreateProject(factory, UserA, "Shop");
            await SaveConversation(factory, shop, "Build a shop", "Which payment provider?");
            var token = Token(factory, UserA);

            string sessionId;
            using (var first = await Client.ConnectAsync(factory, token))
            {
                var (selected, _) = await first.SelectAsync(shop, expected: 2);
                sessionId = Str(selected, "sessionId")!;
            }

            using var second = await Client.ConnectAsync(factory, token, sessionId);
            var resumed = await second.WaitForAsync(m => Str(m, "type") == "session_resumed");
            resumed.Should().NotBeNull("a reconnect with the session id resumes it");
            for (var i = 0; i < 2; i++)
                (await second.WaitForAsync(m => Bool(m, "isReplay") && Str(m, "type") != "session_resumed")).Should().NotBeNull();
            second.Seen.Where(m => Bool(m, "isReplay")).Select(Text).Should().ContainInOrder("Build a shop", "Which payment provider?");
        }
        finally { SafeDispose(factory); }
    }

    [Fact]
    public async Task Going_home_replays_the_callers_own_home_conversation()
    {
        var factory = CreateFactory();
        try
        {
            await SaveHome(factory, UserA, "I want a shop", "What will it sell?");
            await SaveHome(factory, UserB, "B's home idea", "Tell me more.");

            using var a = await Client.ConnectAsync(factory, Token(factory, UserA));
            var (selected, replayed) = await a.SelectHomeAsync(expected: 2);
            selected.GetProperty("messageCount").GetInt32().Should().Be(2);
            replayed.Should().Equal("I want a shop", "What will it sell?");

            using var b = await Client.ConnectAsync(factory, Token(factory, UserB));
            (_, replayed) = await b.SelectHomeAsync(expected: 2);
            replayed.Should().Equal("B's home idea", "Tell me more.");
            b.Seen.Select(Text).Should().NotContain("I want a shop");
        }
        finally { SafeDispose(factory); }
    }

    [Fact]
    public async Task An_empty_home_conversation_starts_with_Dragons_welcome()
    {
        var factory = CreateFactory();
        try
        {
            using var client = await Client.ConnectAsync(factory, Token(factory, UserA));
            var (selected, replayed) = await client.SelectHomeAsync(expected: 0);
            selected.GetProperty("messageCount").GetInt32().Should().Be(0);
            replayed.Should().BeEmpty();

            // No LLM answers here, so the welcome comes back as the error Dragon could not start with; either is the first message
            var first = await client.WaitForAsync(m => Str(m, "type") is "dragon_message" or "error", seconds: 120);
            first.Should().NotBeNull("an empty home conversation is opened by Dragon");
            await WaitUntil(async () => (await SavedTexts(factory.Services.GetRequiredService<SqlHistoryRepository>()
                .LoadHomeHistoryJsonAsync(UserA))).Count == 1);
        }
        finally { SafeDispose(factory); }
    }

    [Fact]
    public async Task A_message_at_home_is_kept_as_the_home_conversation_and_not_the_project_left()
    {
        var factory = CreateFactory();
        try
        {
            var shop = await CreateProject(factory, UserA, "Shop");
            await SaveConversation(factory, shop, "Build a shop", "Which payment provider?");
            await SaveHome(factory, UserA, "Hello", "Hi, what shall we build?");
            var history = factory.Services.GetRequiredService<SqlHistoryRepository>();
            var shopFolder = factory.Services.GetRequiredService<ProjectService>().GetProject(shop)!.Paths.Output;

            using var client = await Client.ConnectAsync(factory, Token(factory, UserA));
            await client.SelectAsync(shop, expected: 2);
            var (_, replayed) = await client.SelectHomeAsync(expected: 2);
            replayed.Should().Equal("Hello", "Hi, what shall we build?");

            // Back in the project: its own conversation, without the home one; then home again
            (_, replayed) = await client.SelectAsync(shop, expected: 2);
            replayed.Should().Equal("Build a shop", "Which payment provider?");
            (_, replayed) = await client.SelectHomeAsync(expected: 2);
            replayed.Should().Equal("Hello", "Hi, what shall we build?");

            // (No LLM answers here, so the message is the last thing this test sends: a switch would wait for its request.)
            await client.SendAsync(new { type = "message", message = "Start a blog" });
            await WaitUntil(async () => (await SavedTexts(history.LoadHomeHistoryJsonAsync(UserA))).Contains("Start a blog"));
            (await SavedTexts(history.LoadHistoryJsonAsync(shopFolder))).Should().Equal("Build a shop", "Which payment provider?");
        }
        finally { SafeDispose(factory); }
    }

    [Fact]
    public async Task A_project_the_home_chat_loads_leaves_the_home_conversation_as_it_is()
    {
        // Found in TASK-149's human test: Sage loading one project and then another from the home chat went through the
        // chat-driven switch, which cleared the session history, and the next message saved that over the home conversation.
        var factory = CreateFactory();
        try
        {
            var shop = await CreateProject(factory, UserA, "Shop");
            var blog = await CreateProject(factory, UserA, "Blog");
            await SaveConversation(factory, shop, "Build a shop", "Which payment provider?");
            var projects = factory.Services.GetRequiredService<ProjectService>();
            var dragon = factory.Services.GetRequiredService<DragonService>();
            var session = new DragonSession { SessionId = "s-home", OwnerSub = UserA, Home = true };
            session.MessageHistory.AddRange(Turns("I want a shop", "What will it sell?"));

            await dragon.OnProjectLoadedAsync(session, projects.GetProject(blog)!.Paths.Output);
            await dragon.OnProjectLoadedAsync(session, projects.GetProject(shop)!.Paths.Output);

            session.MessageHistory.Select(m => Text(JsonSerializer.SerializeToElement(m.Data))).Should()
                .Equal("I want a shop", "What will it sell?");
            session.CurrentProjectFolder.Should().Be(projects.GetProject(shop)!.Paths.Output, "Dragon still works in the loaded project");
        }
        finally { SafeDispose(factory); }
    }

    [Fact]
    public async Task A_new_spec_gets_its_own_folder_after_Dragon_loaded_another_project()
    {
        // TASK-186: Sage's create resolved to the session's folder whenever one was set, so after loading a project
        // every new spec was aimed at that project's folder and refused as "already exists".
        var factory = CreateFactory();
        try
        {
            var shop = await CreateProject(factory, UserA, "Shop");
            var dragon = factory.Services.GetRequiredService<DragonService>();
            var shopFolder = factory.Services.GetRequiredService<ProjectService>().GetProject(shop)!.Paths.Output;
            Directory.CreateDirectory(shopFolder);
            var shopSpec = Path.Combine(shopFolder, "specification.md");
            await File.WriteAllTextAsync(shopSpec, "# Shop");
            var session = new DragonSession { SessionId = "s-1", OwnerSub = UserA, CurrentProjectFolder = shopFolder };

            var folder = dragon.ResolveProjectFolder(session, "todo186");

            Path.GetFullPath(folder).Should().NotBe(Path.GetFullPath(shopFolder));
            Path.GetFileName(folder.TrimEnd('/', '\\')).Should().Be("todo186");
            File.Exists(Path.Combine(folder, "specification.md")).Should().BeFalse("the new project has no spec yet");
            (await File.ReadAllTextAsync(shopSpec)).Should().Be("# Shop");

            // The loaded project's own name still resolves to it, so create answers "use update"
            Path.GetFullPath(dragon.ResolveProjectFolder(session, "Shop")).Should().Be(Path.GetFullPath(shopFolder));
        }
        finally { SafeDispose(factory); }
    }

    [Fact]
    public async Task An_imported_project_waiting_for_its_spec_gets_it_in_its_own_folder()
    {
        var factory = CreateFactory();
        try
        {
            var dragon = factory.Services.GetRequiredService<DragonService>();
            var imported = Path.Combine(_projectsPath, "imported-src");
            Directory.CreateDirectory(imported);
            var session = new DragonSession { SessionId = "s-2", OwnerSub = UserA, CurrentProjectFolder = imported };

            dragon.ResolveProjectFolder(session, "My App").Should().Be(imported);
        }
        finally { SafeDispose(factory); }
        await Task.CompletedTask;
    }

    private sealed class NoCallsProvider : ILlmProvider
    {
        public string Name => "none";
        public Action<string, string>? MessageCallback { get; set; }
        public Task<LlmResponse> SendMessageAsync(List<Message> messages, List<Tool> tools, string systemPrompt,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<LlmStreamingResponse> SendMessageStreamingAsync(List<Message> messages, List<Tool> tools,
            string systemPrompt, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private static List<string> ContextTexts(DragonAgent dragon) =>
        dragon.Conversation.Select(m => m.Content as string ?? "").ToList();

    [Fact]
    public async Task After_a_switch_made_from_the_chat_Dragon_holds_only_the_new_projects_conversation()
    {
        // TASK-180: the chat route (Dragon's spec tool loading another project) set a flag nothing read, so Dragon's
        // LLM context kept the old project's exchange after the switch.
        var factory = CreateFactory();
        try
        {
            var shop = await CreateProject(factory, UserA, "Shop");
            var blog = await CreateProject(factory, UserA, "Blog");
            await SaveConversation(factory, shop, "Build a shop", "Which payment provider?");
            await SaveConversation(factory, blog, "Build a blog", "Static or dynamic?");
            var projects = factory.Services.GetRequiredService<ProjectService>();
            var service = factory.Services.GetRequiredService<DragonService>();
            var dragon = new DragonAgent(new NoCallsProvider(), new AgentOptions { Verbose = false });
            var session = new DragonSession
            {
                SessionId = "s-chat", OwnerSub = UserA, Dragon = dragon,
                CurrentProjectFolder = projects.GetProject(shop)!.Paths.Output
            };
            session.MessageHistory.AddRange(Turns("Build a shop", "Which payment provider?"));
            dragon.RestoreContext([("user", "Build a shop"), ("assistant", "Which payment provider?")]);

            // The request: the user asks for the blog, Dragon's tool loads it, Dragon replies
            await service.OnProjectLoadedAsync(session, projects.GetProject(blog)!.Paths.Output);
            dragon.RestoreContext([("user", "Open the blog"), ("assistant", "Here is the blog.")]); // Dragon's own write-back
            session.MessageHistory.Add(Turns("x", "Here is the blog.")[1]);
            service.ApplyPendingContextReset(session);

            var context = ContextTexts(dragon);
            context.Should().NotContain("Build a shop").And.NotContain("Which payment provider?");
            // The blog's conversation, its last Dragon turn joined with the reply that announced the switch
            context.Should().HaveCount(2);
            context[0].Should().Be("Build a blog");
            context[1].Should().StartWith("Static or dynamic?").And.Contain("Here is the blog.");
            session.PendingContextReset.Should().BeFalse("the reset is consumed");
        }
        finally { SafeDispose(factory); }
    }

    [Fact]
    public async Task The_chat_route_keys_a_project_by_the_folder_select_project_uses()
    {
        var factory = CreateFactory();
        try
        {
            var blog = await CreateProject(factory, UserA, "Blog");
            var projects = factory.Services.GetRequiredService<ProjectService>();
            var service = factory.Services.GetRequiredService<DragonService>();
            var output = projects.GetProject(blog)!.Paths.Output;
            var session = new DragonSession { SessionId = "s-key", OwnerSub = UserA };

            // The spec tool may name the folder another way: here with a trailing separator and in another case
            await service.OnProjectLoadedAsync(session, output.ToUpperInvariant() + Path.DirectorySeparatorChar);

            session.CurrentProjectFolder.Should().Be(output);
        }
        finally { SafeDispose(factory); }
    }

    [Fact]
    public async Task In_a_workspace_a_project_loaded_from_the_chat_is_offered_and_the_session_stays()
    {
        // TASK-190 (decided: ask, and stay until the person agrees). Before, the chat-driven load moved the session to
        // the other project while the web dock kept showing the opened one.
        var factory = CreateFactory();
        try
        {
            var shop = await CreateProject(factory, UserA, "Shop");
            var blog = await CreateProject(factory, UserA, "Blog");
            await SaveConversation(factory, shop, "Build a shop", "Which payment provider?");
            await SaveConversation(factory, blog, "Build a blog", "Static or dynamic?");
            var projects = factory.Services.GetRequiredService<ProjectService>();
            var service = factory.Services.GetRequiredService<DragonService>();
            var shopFolder = projects.GetProject(shop)!.Paths.Output;

            using var client = await Client.ConnectAsync(factory, Token(factory, UserA));
            var (selected, _) = await client.SelectAsync(shop, expected: 2);
            var session = service.GetSession(Str(selected, "sessionId")!)!;
            var contextBefore = ContextTexts(session.Dragon!);

            await service.OnProjectLoadedAsync(session, projects.GetProject(blog)!.Paths.Output);

            var offer = await client.WaitForAsync(m => Str(m, "type") == "project_switch_offered");
            offer.Should().NotBeNull("the dock is offered the project Dragon loaded");
            Str(offer!.Value, "projectId").Should().Be(blog);
            Str(offer.Value, "projectName").Should().Be("Blog");
            session.CurrentProjectFolder.Should().Be(shopFolder, "the session stays on the project the person opened");
            session.PendingContextReset.Should().BeFalse();
            session.MessageHistory.Select(m => Text(JsonSerializer.SerializeToElement(m.Data))).Should()
                .Equal("Build a shop", "Which payment provider?");
            ContextTexts(session.Dragon!).Should().Equal(contextBefore);

            // Accepting is the client's own select_project: Dragon then follows with the blog's conversation
            var (_, replayed) = await client.SelectAsync(blog, expected: 2);
            replayed.Should().Equal("Build a blog", "Static or dynamic?");
            session.CurrentProjectFolder.Should().Be(projects.GetProject(blog)!.Paths.Output);
        }
        finally { SafeDispose(factory); }
    }

    [Fact]
    public async Task A_project_the_caller_cannot_see_is_not_offered()
    {
        var factory = CreateFactory();
        try
        {
            var shop = await CreateProject(factory, UserA, "Shop");
            var theirs = await CreateProject(factory, UserB, "Secret");
            var projects = factory.Services.GetRequiredService<ProjectService>();
            var service = factory.Services.GetRequiredService<DragonService>();

            using var client = await Client.ConnectAsync(factory, Token(factory, UserA));
            var (selected, _) = await client.SelectAsync(shop, expected: 0);
            var session = service.GetSession(Str(selected, "sessionId")!)!;

            await service.OnProjectLoadedAsync(session, projects.GetProject(theirs)!.Paths.Output);

            (await client.WaitForAsync(m => Str(m, "type") == "project_switch_offered", seconds: 3)).Should().BeNull();
            session.CurrentProjectFolder.Should().Be(projects.GetProject(shop)!.Paths.Output);
        }
        finally { SafeDispose(factory); }
    }

    private static async Task WaitUntil(Func<Task<bool>> condition, int seconds = 10)
    {
        var until = DateTime.UtcNow.AddSeconds(seconds);
        while (!await condition())
        {
            DateTime.UtcNow.Should().BeBefore(until, "the condition should hold within {0} s", seconds);
            await Task.Delay(100);
        }
    }
}
