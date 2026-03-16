using Birko.Validation.Fluent;
using DraCode.KoboldLair.Models.Configuration;

namespace DraCode.KoboldLair.Validation;

/// <summary>
/// Validates AgentsConfig to ensure all agent configurations have sensible limits.
/// </summary>
public class ProjectConfigValidator : AbstractValidator<AgentsConfig>
{
    public ProjectConfigValidator()
    {
        // Wyrm agent config
        RuleFor(x => x.Wyrm.MaxParallel).Range(0, 50);
        RuleFor(x => x.Wyrm.Timeout).GreaterThanOrEqual(0);

        // Wyvern agent config
        RuleFor(x => x.Wyvern.MaxParallel).Range(0, 50);
        RuleFor(x => x.Wyvern.Timeout).GreaterThanOrEqual(0);

        // Drake agent config
        RuleFor(x => x.Drake.MaxParallel).Range(0, 50);
        RuleFor(x => x.Drake.Timeout).GreaterThanOrEqual(0);

        // KoboldPlanner agent config
        RuleFor(x => x.KoboldPlanner.MaxParallel).Range(0, 50);
        RuleFor(x => x.KoboldPlanner.Timeout).GreaterThanOrEqual(0);

        // Kobold agent config
        RuleFor(x => x.Kobold.MaxParallel).Range(0, 50);
        RuleFor(x => x.Kobold.Timeout).GreaterThanOrEqual(0);
    }
}
