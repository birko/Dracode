using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Birko.Security;
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

namespace DraCode.KoboldLair.Tests.Api;

/// <summary>
/// A project's branches over REST (TASK-156): the branch list with ahead/behind main and whether each merges cleanly,
/// and a branch's diff file by file; read-only (the working tree and current branch are untouched), owner-scoped
/// (another owner's project is 404), a branch the repository lacks is 404. Real git repositories in the test folder.
/// </summary>
public class GitEndpointsTests : IDisposable
{
    private const string Secret = "test-secret-key-at-least-32-characters-long-xyz";
    private const string GitHubOwner = "github:4242";
    private const string OtherOwner = "22222222-2222-2222-2222-222222222222";
    private const string OwnerScopes = "view_own,manage_projects,execute_agents";

    private readonly string _projectsPath = Path.Combine(Path.GetTempPath(), "kl-git-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(_projectsPath))
            {
                foreach (var f in Directory.EnumerateFiles(_projectsPath, "*", SearchOption.AllDirectories))
                    File.SetAttributes(f, FileAttributes.Normal); // git object files are read-only
                Directory.Delete(_projectsPath, recursive: true);
            }
        }
        catch { }
    }

    private WebApplicationFactory<Program> CreateFactory()
    {
        var config = new Dictionary<string, string?>
        {
            ["Authentication:Jwt:Enabled"] = "true",
            ["Authentication:Jwt:Secret"] = Secret,
            ["Authentication:Jwt:Issuer"] = "KoboldLair",
            ["Authentication:Jwt:Audience"] = "KoboldLair",
            ["KoboldLair:ProjectsPath"] = _projectsPath,
        };
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(config));
            builder.ConfigureTestServices(services => services.RemoveAll<IHostedService>());
        });
    }

    private static string Token(WebApplicationFactory<Program> factory, string sub, string scope = OwnerScopes) =>
        factory.Services.GetRequiredService<ITokenProvider>().GenerateToken(new Dictionary<string, string>
        {
            ["sub"] = sub,
            ["scope"] = scope
        }).Token;

    private static HttpRequestMessage Authed(HttpMethod method, string uri, string token) =>
        new(method, uri) { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) } };

    private static async Task<string> CreateProject(HttpClient client, string token, string name)
    {
        var request = Authed(HttpMethod.Post, "/api/v1/projects", token);
        request.Content = JsonContent.Create(new { name });
        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetString()!;
    }

    private static string Git(string folder, params string[] args)
    {
        var psi = new ProcessStartInfo("git") { WorkingDirectory = folder, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var a in new[] { "-c", "user.name=test", "-c", "user.email=test@example.com", "-c", "core.autocrlf=false" }.Concat(args))
            psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var output = p.StandardOutput.ReadToEnd();
        p.StandardError.ReadToEnd();
        p.WaitForExit();
        return output;
    }

    /// <summary>Lines between app.js's first and last, so changes to those two do not touch (adjacent changes conflict).</summary>
    private const string Filler = "// one\n// two\n// three\n// four\n";

    private static void Write(string folder, string file, string content) =>
        File.WriteAllText(Path.Combine(folder, file), content.Replace("\r\n", "\n"));

    /// <summary>
    /// The project's repository: main with app.js; feature/f1-login adds login.js and changes app.js (merges cleanly);
    /// feature/f2-greeting changes app.js's first line, which main then changes too (conflicts). main is checked out.
    /// </summary>
    private static string SeedRepository(WebApplicationFactory<Program> factory, string projectId)
    {
        var projects = factory.Services.GetRequiredService<ProjectService>();
        var folder = projects.ResolveProjectFolder(projects.GetProject(projectId)!)!;
        if (!Directory.Exists(Path.Combine(folder, ".git"))) Git(folder, "init", "-q", "-b", "main");
        Write(folder, "app.js", "const greeting = 'hello';\n" + Filler + "module.exports = greeting;\n");
        Git(folder, "add", "-A");
        Git(folder, "commit", "-q", "-m", "init");

        Git(folder, "checkout", "-q", "-b", "feature/f1-login");
        Write(folder, "login.js", "export function login() {}\n");
        Write(folder, "app.js", "const greeting = 'hello';\n" + Filler + "module.exports = { greeting };\n");
        Git(folder, "add", "-A");
        Git(folder, "commit", "-q", "-m", "Add login");
        Write(folder, "login.js", "export function login() { return true; }\n");
        Git(folder, "commit", "-q", "-am", "Login returns true");

        Git(folder, "checkout", "-q", "main");
        Git(folder, "checkout", "-q", "-b", "feature/f2-greeting");
        Write(folder, "app.js", "const greeting = 'hi';\n" + Filler + "module.exports = greeting;\n");
        Git(folder, "commit", "-q", "-am", "Say hi");

        Git(folder, "checkout", "-q", "main");
        Write(folder, "app.js", "const greeting = 'hey';\n" + Filler + "module.exports = greeting;\n");
        Git(folder, "commit", "-q", "-am", "Say hey");
        return folder;
    }

    private static async Task<JsonElement> Get(HttpClient client, string token, string path)
    {
        var response = await client.SendAsync(Authed(HttpMethod.Get, path, token));
        response.StatusCode.Should().Be(HttpStatusCode.OK, path);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    [Fact]
    public async Task The_owner_gets_the_branches_with_ahead_behind_and_merge_state_and_nothing_in_the_repository_changes()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var token = Token(factory, GitHubOwner);
        var id = await CreateProject(client, token, "Shop");
        var folder = SeedRepository(factory, id);
        Write(folder, "scratch.txt", "uncommitted work\n"); // must survive the reads
        var head = Git(folder, "rev-parse", "HEAD");

        var listed = await Get(client, token, $"/api/v1/projects/{id}/git/branches");

        listed.GetProperty("repository").GetBoolean().Should().BeTrue();
        var branches = listed.GetProperty("branches").EnumerateArray().ToDictionary(b => b.GetProperty("name").GetString()!);
        branches.Keys.Should().BeEquivalentTo("feature/f1-login", "feature/f2-greeting");
        var login = branches["feature/f1-login"];
        login.GetProperty("ahead").GetInt32().Should().Be(2);
        login.GetProperty("behind").GetInt32().Should().Be(1);
        login.GetProperty("mergesCleanly").GetBoolean().Should().BeTrue();
        login.GetProperty("featureId").GetString().Should().Be("f1");
        login.GetProperty("lastCommitMessage").GetString().Should().Be("Login returns true");
        var greeting = branches["feature/f2-greeting"];
        greeting.GetProperty("mergesCleanly").GetBoolean().Should().BeFalse();
        greeting.GetProperty("conflicts").EnumerateArray().Select(c => c.GetString()).Should().Equal("app.js");

        Git(folder, "branch", "--show-current").Trim().Should().Be("main", "a read checks nothing out");
        Git(folder, "rev-parse", "HEAD").Should().Be(head);
        File.ReadAllText(Path.Combine(folder, "scratch.txt")).Should().Be("uncommitted work\n");
        Git(folder, "status", "--porcelain").Trim().Should().Be("?? scratch.txt", "no merge left behind");
    }

    [Fact]
    public async Task A_branch_diff_comes_file_by_file_and_matches_git_diff_main_three_dots()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var token = Token(factory, GitHubOwner);
        var id = await CreateProject(client, token, "Shop");
        var folder = SeedRepository(factory, id);

        var diff = await Get(client, token, $"/api/v1/projects/{id}/git/branches/{Uri.EscapeDataString("feature/f1-login")}/diff");

        diff.GetProperty("branch").GetString().Should().Be("feature/f1-login");
        var files = diff.GetProperty("files").EnumerateArray().ToDictionary(f => f.GetProperty("path").GetString()!);
        files.Keys.Should().BeEquivalentTo("app.js", "login.js");
        files["login.js"].GetProperty("status").GetString().Should().Be("Added");
        files["login.js"].GetProperty("added").GetInt32().Should().Be(1);
        files["app.js"].GetProperty("status").GetString().Should().Be("Modified");
        files["app.js"].GetProperty("added").GetInt32().Should().Be(1);
        files["app.js"].GetProperty("deleted").GetInt32().Should().Be(1);
        var patch = files["app.js"].GetProperty("patch").GetString()!;
        patch.Should().StartWith("diff --git a/app.js b/app.js");
        patch.Should().Contain("+module.exports = { greeting };").And.Contain("-module.exports = greeting;");
        patch.Should().NotContain("hey", "the three-dot diff leaves out what main did since the branch left it");

        var whole = Git(folder, "diff", "--no-color", "main...feature/f1-login").Replace("\r", "").TrimEnd('\n');
        string.Join("\n", files.Values.OrderBy(f => whole.IndexOf(f.GetProperty("path").GetString()!, StringComparison.Ordinal))
            .Select(f => f.GetProperty("patch").GetString())).Should().Be(whole);
    }

    private static Task<HttpResponseMessage> Merge(HttpClient client, string token, string id, string branch) =>
        client.SendAsync(Authed(HttpMethod.Post, $"/api/v1/projects/{id}/git/branches/{Uri.EscapeDataString(branch)}/merge", token));

    [Fact]
    public async Task Merging_a_clean_branch_merges_it_into_main_deletes_it_and_retires_its_ready_notification()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var token = Token(factory, GitHubOwner);
        var id = await CreateProject(client, token, "Shop");
        var folder = SeedRepository(factory, id);
        var notifications = factory.Services.GetRequiredService<DraCode.KoboldLair.Server.Services.ProjectNotificationService>();
        notifications.NotifyFeatureBranchReady("Shop", "login", "feature/f1-login");
        notifications.NotifyFeatureBranchReady("Shop", "greeting", "feature/f2-greeting");

        var response = await Merge(client, token, id, "feature/f1-login");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
        {
            doc.RootElement.GetProperty("merged").GetBoolean().Should().BeTrue();
            doc.RootElement.GetProperty("deleted").GetBoolean().Should().BeTrue();
        }
        Git(folder, "log", "--format=%s", "main").Should().Contain("Login returns true").And.Contain("Merge branch 'feature/f1-login' into main");
        Git(folder, "branch", "--list", "feature/f1-login").Trim().Should().BeEmpty("a merged branch is deleted");
        File.ReadAllText(Path.Combine(folder, "app.js")).Should().Contain("module.exports = { greeting };").And.Contain("'hey'");
        notifications.GetPendingNotifications("Shop").Select(n => n.Metadata["branchName"]).Should().Equal(new[] { "feature/f2-greeting" },
            "the merged branch's ready-to-merge notification is read; the other stays");
    }

    [Fact]
    public async Task A_conflicting_branch_answers_409_with_the_files_and_leaves_the_repository_untouched()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var token = Token(factory, GitHubOwner);
        var id = await CreateProject(client, token, "Shop");
        var folder = SeedRepository(factory, id);
        Write(folder, "scratch.txt", "uncommitted work\n");
        var head = Git(folder, "rev-parse", "HEAD");
        var branches = Git(folder, "branch", "--list");

        var response = await Merge(client, token, id, "feature/f2-greeting");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using (var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
            doc.RootElement.GetProperty("conflicts").EnumerateArray().Select(c => c.GetString()).Should().Equal("app.js");
        Git(folder, "rev-parse", "HEAD").Should().Be(head);
        Git(folder, "branch", "--list").Should().Be(branches, "nothing was deleted");
        Git(folder, "status", "--porcelain").Trim().Should().Be("?? scratch.txt", "no merge left behind");
    }

    [Fact]
    public async Task Merging_needs_the_project_and_the_permission_and_a_real_branch_other_than_main()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var mine = Token(factory, GitHubOwner);
        var theirs = Token(factory, OtherOwner);
        var secret = await CreateProject(client, theirs, "Secret");
        var folder = SeedRepository(factory, secret);

        (await Merge(client, mine, secret, "feature/f1-login")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Merge(client, Token(factory, OtherOwner, "view_own"), secret, "feature/f1-login")).StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "merging needs manage_projects");
        (await Merge(client, theirs, secret, "nope")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Merge(client, theirs, secret, "main")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        Git(folder, "branch", "--list", "feature/f1-login").Trim().Should().NotBeEmpty();

        (await Merge(client, Token(factory, "admin", "*"), secret, "feature/f1-login")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Another_owners_project_and_an_unknown_branch_are_not_found_and_an_admin_sees_all()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var mine = Token(factory, GitHubOwner);
        var theirs = Token(factory, OtherOwner);
        var shop = await CreateProject(client, mine, "Shop");
        var secret = await CreateProject(client, theirs, "Secret");
        SeedRepository(factory, secret);

        (await client.SendAsync(Authed(HttpMethod.Get, $"/api/v1/projects/{secret}/git/branches", mine))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.SendAsync(Authed(HttpMethod.Get, $"/api/v1/projects/{secret}/git/branches/{Uri.EscapeDataString("feature/f1-login")}/diff", mine)))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.SendAsync(Authed(HttpMethod.Get, $"/api/v1/projects/{secret}/git/branches/{Uri.EscapeDataString("--output=x")}/diff", theirs)))
            .StatusCode.Should().Be(HttpStatusCode.NotFound, "only a branch the repository has reaches git");
        (await client.SendAsync(Authed(HttpMethod.Get, $"/api/v1/projects/{secret}/git/branches/nope/diff", theirs)))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        var admin = Token(factory, "admin", "*");
        (await Get(client, admin, $"/api/v1/projects/{secret}/git/branches")).GetProperty("branches").GetArrayLength().Should().Be(2);
        (await Get(client, mine, $"/api/v1/projects/{shop}/git/branches")).GetProperty("branches").GetArrayLength().Should().Be(0);
    }
}
