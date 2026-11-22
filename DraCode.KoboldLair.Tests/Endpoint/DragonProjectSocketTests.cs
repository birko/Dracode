using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Birko.Security;
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
/// Only the caller's own projects can be selected.
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
}
