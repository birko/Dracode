using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Services;
using FluentAssertions;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// The whole-task acceptance check (TASK-025, FEATURE-031 D2): a task is held to what its own description promises.
/// A missing target file fails; a declared export missing from its existing target file fails; anything a machine
/// cannot settle (a prose criterion, a clause that will not parse, a name found only in another file) goes to a person.
/// </summary>
public class TaskAcceptanceServiceTests : IDisposable
{
    private readonly string _workspace = Path.Combine(Path.GetTempPath(), $"dracode_accept_{Guid.NewGuid():N}");
    private readonly TaskAcceptanceService _service = new();

    // Wyvern's own GOOD example (WyvernAgent.cs)
    private const string AuthTask =
        "Create authentication service (src/services/AuthService.ts). Must implement: login with email/password, " +
        "JWT token generation with 24h expiry, bcrypt password hashing (min 10 rounds), refresh token rotation. " +
        "Constraints: no third-party auth libraries. Exports: AuthService class with login(email, password): " +
        "Promise<TokenPair>, refresh(token): Promise<TokenPair>, validateToken(jwt): Promise<User|null>";

    public TaskAcceptanceServiceTests() => Directory.CreateDirectory(_workspace);

    public void Dispose()
    {
        try { Directory.Delete(_workspace, recursive: true); } catch { }
    }

    private void Write(string relativePath, string content)
    {
        var path = Path.Combine(_workspace, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static TaskRecord Task(string text) => new() { Task = text };

    [Fact]
    public async Task A_declared_export_missing_from_its_target_file_fails_and_is_named()
    {
        Write("src/services/AuthService.ts",
            "export class AuthService {\n  async login(email: string, password: string) {}\n  async validateToken(jwt: string) {}\n}\n");

        var result = await _service.EvaluateAsync(Task(AuthTask), _workspace);

        result.Passed.Should().BeFalse();
        result.Unmet.Should().ContainSingle().Which.Should().Contain("refresh").And.Contain("src/services/AuthService.ts");
    }

    [Fact]
    public async Task Every_declared_export_present_leaves_only_the_prose_for_a_person()
    {
        Write("src/services/AuthService.ts",
            "export class AuthService {\n  login(email, password) {}\n  refresh = (token) => {};\n  validateToken(jwt) {}\n}\n");

        var result = await _service.EvaluateAsync(Task(AuthTask), _workspace);

        result.Unmet.Should().BeEmpty();
        result.NeedsHuman.Should().Contain("refresh token rotation").And.Contain("login with email/password");
        result.Passed.Should().BeFalse("prose criteria are a person's to judge, never auto-passed");
    }

    [Fact]
    public async Task A_missing_target_file_fails()
    {
        var result = await _service.EvaluateAsync(Task(AuthTask), _workspace);

        result.Unmet.Should().Contain(u => u.Contains("src/services/AuthService.ts") && u.Contains("missing"));
    }

    [Fact]
    public async Task An_empty_target_file_fails()
    {
        Write("src/services/AuthService.ts", "");

        var result = await _service.EvaluateAsync(Task(AuthTask), _workspace);

        result.Unmet.Should().Contain(u => u.Contains("src/services/AuthService.ts") && u.Contains("empty"));
    }

    [Fact]
    public async Task A_task_with_only_files_and_exports_passes_when_they_are_there()
    {
        Write("src/util/slug.ts", "export function slugify(text: string): string { return text; }\n");

        var result = await _service.EvaluateAsync(Task("Create the slug helper (src/util/slug.ts). Exports: slugify(text): string"), _workspace);

        result.Passed.Should().BeTrue();
        result.Checked.Should().Contain(c => c.Contains("slugify"));
    }

    [Fact]
    public async Task A_name_found_only_in_another_file_goes_to_a_person_rather_than_failing()
    {
        Write("src/util/slug.ts", "export const placeholder = 1;\n");
        Write("src/util/text.ts", "export function slugify(text: string) { return text; }\n");

        var result = await _service.EvaluateAsync(Task("Create the slug helper (src/util/slug.ts). Exports: slugify(text): string"), _workspace);

        result.Unmet.Should().BeEmpty();
        result.NeedsHuman.Should().Contain(n => n.Contains("slugify") && n.Contains("src/util/text.ts"));
    }

    [Fact]
    public async Task An_exports_clause_that_will_not_parse_goes_to_a_person()
    {
        Write("src/util/slug.ts", "export function slugify(text: string) { return text; }\n");

        var result = await _service.EvaluateAsync(Task("Create the slug helper (src/util/slug.ts). Exports: (whatever the router needs)"), _workspace);

        result.Unmet.Should().BeEmpty();
        result.NeedsHuman.Should().Contain(n => n.Contains("whatever the router needs"));
    }

    [Fact]
    public async Task Framework_names_are_not_taken_for_files()
    {
        Write("server/app.js", "module.exports = function start() {};\n");

        var result = await _service.EvaluateAsync(Task("Set up the Node.js and Express.js server in server/app.js."), _workspace);

        result.Unmet.Should().BeEmpty();
        result.Checked.Should().Contain(c => c.Contains("server/app.js"));
    }

    [Fact]
    public async Task A_python_export_is_found_by_its_def()
    {
        Write("app/models.py", "class User:\n    pass\n\ndef create_user(name):\n    return User()\n");

        var result = await _service.EvaluateAsync(Task("Add the user model (app/models.py). Exports: User class, create_user(name)"), _workspace);

        result.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task A_dot_folder_keeps_its_dot_and_a_path_out_of_the_workspace_is_never_read()
    {
        Write(".github/workflows/ci.yml", "on: push\n");

        var result = await _service.EvaluateAsync(
            Task("Add CI (.github/workflows/ci.yml) and reuse ../shared/secrets.json from the parent."), _workspace);

        result.Unmet.Should().BeEmpty();
        result.Checked.Should().ContainSingle().Which.Should().Contain(".github/workflows/ci.yml");
    }

    [Fact]
    public async Task A_reopened_task_is_judged_on_its_promises_not_on_the_notes_its_reopens_appended()
    {
        Write("src/util/slug.ts", "export function slugify(text: string): string { return text; }\n");
        var reopened = "Create the slug helper (src/util/slug.ts). Exports: slugify(text): string\n\n" +
                       $"{TaskAcceptanceService.ReopenNotePrefix} 1): Export slugify is not declared in src/util/other.ts.";

        var result = await _service.EvaluateAsync(Task(reopened), _workspace);

        result.Passed.Should().BeTrue("the fixed task now keeps every promise its description made");
    }

    [Fact]
    public async Task A_task_that_promises_nothing_checkable_passes()
    {
        var result = await _service.EvaluateAsync(Task("Tidy up the logging."), _workspace);

        result.Passed.Should().BeTrue();
        result.Checked.Should().BeEmpty();
    }
}
