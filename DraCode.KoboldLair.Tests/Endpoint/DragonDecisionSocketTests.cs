using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Birko.Security;
using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Models.Tasks;
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
/// The <c>/dragon</c> socket carries a person's pending decisions both ways (TASK-024, FEATURE-030): every open decision
/// of the caller's projects arrives as a <c>decision_required</c> card when the client is ready, again on each connect
/// until answered (D6); a new one is pushed to every session of a user who can see its project (D2); and
/// <c>answer_decision</c> answers it, once (D5), only for the caller's own projects.
/// </summary>
public class DragonDecisionSocketTests : IDisposable
{
    private const string Secret = "test-secret-key-at-least-32-characters-long-xyz";
    private const string UserA = "11111111-1111-1111-1111-111111111111";
    private const string UserB = "22222222-2222-2222-2222-222222222222";
    private const string OwnerScopes = "view_own,manage_projects,execute_agents";

    private readonly string _projectsPath = Path.Combine(Path.GetTempPath(), "kl-decision-socket", Guid.NewGuid().ToString("N"));

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
            // A provider so a Dragon session can create its agents; nothing reachable answers it.
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

    private static async Task<(string Id, string Name)> CreateProject(WebApplicationFactory<Program> factory, string sub, string name)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/projects")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", Token(factory, sub)) },
            Content = JsonContent.Create(new { name })
        };
        var response = await factory.CreateClient().SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (doc.RootElement.GetProperty("id").GetString()!, name);
    }

    private static async Task<PendingDecision> Ask(WebApplicationFactory<Program> factory, string projectId,
        string question = "Which database?", params string[] options)
    {
        var decision = new PendingDecision
        {
            ProjectId = projectId, TaskId = Guid.NewGuid().ToString(), Source = PendingDecisionSource.AskHuman,
            Question = new AskHumanQuestion(question, options, options.Length == 0, "the spec names neither").ToJson()
        };
        await factory.Services.GetRequiredService<SqlPendingDecisionRepository>().CreateAsync(decision);
        return decision;
    }

    private sealed class Client(WebSocket socket) : IDisposable
    {
        private readonly List<JsonElement> _seen = new();

        /// <param name="ready">Send <c>client_ready</c>, which brings the open decisions and Dragon's welcome. Answers queue
        /// behind that welcome, which here retries a model nobody serves, so a client that only answers skips it.</param>
        public static async Task<Client> ConnectAsync(WebApplicationFactory<Program> factory, string token, bool ready = true)
        {
            var uri = new UriBuilder(factory.Server.BaseAddress) { Scheme = "ws", Path = "/dragon", Query = $"token={token}" }.Uri;
            var client = new Client(await factory.Server.CreateWebSocketClient().ConnectAsync(uri, CancellationToken.None));
            if (ready)
                await client.SendAsync(new { type = "client_ready" });
            return client;
        }

        public Task SendAsync(object message) => socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(message),
            WebSocketMessageType.Text, true, CancellationToken.None);

        /// <summary>The first message matching <paramref name="match"/>, waiting up to <paramref name="seconds"/>; null if none came.</summary>
        public async Task<JsonElement?> WaitForAsync(Func<JsonElement, bool> match, int seconds = 10)
        {
            var hit = _seen.FirstOrDefault(match);
            if (hit.ValueKind != JsonValueKind.Undefined)
                return hit;
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

        public Task<JsonElement?> CardAsync(string decisionId, int seconds = 10) => WaitForAsync(m =>
            Str(m, "type") == "project_notification" && Str(m, "notificationType") == PendingDecisionNotification.RequiredType
            && m.TryGetProperty("metadata", out var meta) && Str(meta, PendingDecisionNotification.DecisionId) == decisionId, seconds);

        public async Task<JsonElement> AnswerAsync(string decisionId, string answer)
        {
            await SendAsync(new { type = "answer_decision", decisionId, answer });
            var reply = await WaitForAsync(m => Str(m, "type") == "decision_answer_result" && Str(m, "decisionId") == decisionId
                && !_answered.Contains(m));
            reply.Should().NotBeNull("the server replies to every answer");
            _answered.Add(reply!.Value);
            return reply.Value;
        }

        private readonly List<JsonElement> _answered = new();

        public void Dispose() => socket.Dispose();
    }

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    [Fact]
    public async Task An_open_decision_arrives_as_a_card_on_every_connect_until_it_is_answered()
    {
        var factory = CreateFactory();
        try
        {
            var project = await CreateProject(factory, UserA, "Shop");
            var asked = await Ask(factory, project.Id, "Which database?", "SQLite", "Postgres");

            using (var first = await Client.ConnectAsync(factory, Token(factory, UserA)))
            {
                var card = await first.CardAsync(asked.Id);
                card.Should().NotBeNull("an open decision is offered when the client is ready");
                Str(card!.Value.GetProperty("metadata"), PendingDecisionNotification.ResponseSchema).Should().Contain("Postgres");
            }

            using (var second = await Client.ConnectAsync(factory, Token(factory, UserA)))
                (await second.CardAsync(asked.Id)).Should().NotBeNull("an unanswered decision comes back on every connect");

            using (var answering = await Client.ConnectAsync(factory, Token(factory, UserA), ready: false))
                Str(await answering.AnswerAsync(asked.Id, "Postgres"), "result").Should().Be(nameof(DecisionAnswerResult.Resolved));

            using var third = await Client.ConnectAsync(factory, Token(factory, UserA));
            (await third.CardAsync(asked.Id, seconds: 3)).Should().BeNull("an answered decision is not offered again");
        }
        finally { SafeDispose(factory); }
    }

    [Fact]
    public async Task Answering_stores_the_answer_once_and_a_second_answer_says_nothing_changed()
    {
        var factory = CreateFactory();
        try
        {
            var project = await CreateProject(factory, UserA, "Shop");
            var asked = await Ask(factory, project.Id, "Which database?", "SQLite", "Postgres");
            using var client = await Client.ConnectAsync(factory, Token(factory, UserA), ready: false);

            var invalid = await client.AnswerAsync(asked.Id, "MySQL");
            var first = await client.AnswerAsync(asked.Id, " Postgres ");
            var second = await client.AnswerAsync(asked.Id, "SQLite");

            Str(invalid, "result").Should().Be(nameof(DecisionAnswerResult.InvalidAnswer));
            Str(invalid, "message").Should().Contain("Postgres");
            Str(first, "result").Should().Be(nameof(DecisionAnswerResult.Resolved));
            Str(second, "result").Should().Be(nameof(DecisionAnswerResult.AlreadyResolved));
            Str(second, "message").Should().Contain("nothing changed");
            var stored = await factory.Services.GetRequiredService<SqlPendingDecisionRepository>().GetByIdAsync(asked.Id);
            stored!.Resolution.Should().Be("Postgres");
        }
        finally { SafeDispose(factory); }
    }

    [Fact]
    public async Task Another_users_decision_is_neither_offered_nor_answerable()
    {
        var factory = CreateFactory();
        try
        {
            var project = await CreateProject(factory, UserA, "Shop");
            var asked = await Ask(factory, project.Id, "Their secret question?");
            using var intruder = await Client.ConnectAsync(factory, Token(factory, UserB));

            (await intruder.CardAsync(asked.Id, seconds: 3)).Should().BeNull();
            using var answering = await Client.ConnectAsync(factory, Token(factory, UserB), ready: false);
            var reply = await answering.AnswerAsync(asked.Id, "yes");

            Str(reply, "result").Should().Be(nameof(DecisionAnswerResult.NotFound));
            Str(reply, "message").Should().NotContain("Their secret question?");
            var stored = await factory.Services.GetRequiredService<SqlPendingDecisionRepository>().GetByIdAsync(asked.Id);
            stored!.IsResolved.Should().BeFalse();
        }
        finally { SafeDispose(factory); }
    }

    [Fact]
    public async Task A_new_decision_is_pushed_to_a_session_of_its_owner_that_is_not_viewing_the_project()
    {
        var factory = CreateFactory();
        try
        {
            var project = await CreateProject(factory, UserA, "Shop");
            using var owner = await Client.ConnectAsync(factory, Token(factory, UserA));
            using var other = await Client.ConnectAsync(factory, Token(factory, UserB));
            await owner.WaitForAsync(_ => false, seconds: 2); // let client_ready finish before the decision exists

            var asked = await Ask(factory, project.Id);
            factory.Services.GetRequiredService<DrakeFactory>().OnDecisionRequired!(project.Name, asked);

            (await owner.CardAsync(asked.Id)).Should().NotBeNull("the owner hears of it without opening the project");
            (await other.CardAsync(asked.Id, seconds: 3)).Should().BeNull("a user who cannot see the project is not told");
        }
        finally { SafeDispose(factory); }
    }
}
