using Birko.Validation.Fluent;
using DraCode.KoboldLair.Models.Projects;

namespace DraCode.KoboldLair.Validation;

/// <summary>
/// Validates Specification instances before persistence.
/// </summary>
public class SpecificationValidator : AbstractValidator<Specification>
{
    public SpecificationValidator()
    {
        RuleFor(x => x.Name).Required().MaxLength(200);
        RuleFor(x => x.Content).Required().MinLength(10, "Specification must have meaningful content");
        RuleFor(x => x.FilePath).Required();
    }
}
