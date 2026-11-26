using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// What the person defines is written into the specification as a new version (TASK-207, FEATURE-078 D20): an answer to
/// a question that climbed past Dragon (the specification did not cover it) is added under "## Definitions", the version
/// is bumped like any specification change, so the next such question is answered from the specification.
/// </summary>
public class SpecificationDefinitionTests : IAsyncLifetime
{
    private const string ProjectId = "proj-define";
    private string _dir = null!;
    private SpecificationService _specs = null!;
    private SqlPendingDecisionRepository _decisions = null!;

    public async Task InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"dracode_define_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        _specs = new SpecificationService(_dir);
        await _specs.SaveContentAsync("greet", "# Greeter\n\nPrints Hello.\n\n## Out of scope\n\n- A GUI\n");
        _decisions = new SqlPendingDecisionRepository(Path.Combine(_dir, "koboldlair.db"));
        await _decisions.InitializeAsync();
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { }
        return Task.CompletedTask;
    }

    [Fact]
    public async Task A_definition_is_added_under_Definitions_as_a_new_version_and_later_ones_join_it()
    {
        var before = (await _specs.LoadByNameAsync("greet"))!.Version;

        await _specs.AppendDefinitionAsync("greet", "Which license should greet.py be under?", "MIT");
        await _specs.AppendDefinitionAsync("greet", "Who maintains it?", "Jana");

        var spec = (await _specs.LoadByNameAsync("greet"))!;
        spec.Version.Should().Be(before + 2, "each definition is a specification change");
        spec.Content.Should().Contain("## Out of scope\n\n- A GUI");
        var definitions = spec.Content[spec.Content.IndexOf("## Definitions", StringComparison.Ordinal)..];
        definitions.Should().Contain("- Which license should greet.py be under? — MIT")
            .And.Contain("- Who maintains it? — Jana");
        System.Text.RegularExpressions.Regex.Matches(spec.Content, "## Definitions").Should().HaveCount(1);
    }

    [Fact]
    public async Task A_definition_for_a_project_with_no_specification_is_dropped()
    {
        var append = () => _specs.AppendDefinitionAsync("nowhere", "Q?", "A");

        await append.Should().NotThrowAsync();
        (await _specs.LoadByNameAsync("nowhere")).Should().BeNull();
    }

    [Fact]
    public async Task The_persons_answer_to_a_question_Dragon_passed_on_is_written_into_the_specification()
    {
        var defined = new List<(string Project, string Question, string Answer)>();
        var dragon = new DragonLevel(_ => "# Greeter", _decisions, _ => null,
            recordDefinition: (project, question, answer) => { defined.Add((project, question, answer)); return Task.CompletedTask; });
        var chain = new QuestionChain().Register(dragon);
        var question = new ChainQuestion(ProjectId, null, QuestionLevel.Overseer,
            new AskHumanQuestion("Which license should greet.py be under?", ["MIT", "Apache-2.0"], false, null));
        var outcome = await chain.AskAsync(question);

        await chain.DeliverAnswerAsync(question, outcome.Path, "MIT", QuestionLevel.Person);

        defined.Should().ContainSingle().Which.Should().Be((ProjectId, "Which license should greet.py be under?", "MIT"));
    }
}
