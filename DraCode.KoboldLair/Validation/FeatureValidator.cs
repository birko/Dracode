using Birko.Validation.Fluent;
using DraCode.KoboldLair.Models.Tasks;

namespace DraCode.KoboldLair.Validation;

/// <summary>
/// Validates Feature instances before persistence.
/// </summary>
public class FeatureValidator : AbstractValidator<Feature>
{
    public FeatureValidator()
    {
        RuleFor(x => x.Name).Required().MaxLength(200);
        RuleFor(x => x.Description).Required().MinLength(5);
        RuleFor(x => x.Status).Must(
            status => Enum.IsDefined(typeof(FeatureStatus), status),
            "Status must be a valid FeatureStatus value");
    }
}
