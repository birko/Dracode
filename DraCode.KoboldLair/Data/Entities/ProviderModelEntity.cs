using Birko.Data.Models;
using Birko.Data.SQL.Attributes;

namespace DraCode.KoboldLair.Data.Entities
{
    /// <summary>
    /// One switchable model offered by a provider (TASK-073). A provider (<see cref="ProviderConfigEntity"/>)
    /// has many of these — the picklist of models a user/agent can switch between. Linked by
    /// <see cref="ProviderName"/>. The provider's <see cref="ProviderConfigEntity.DefaultModel"/> names which
    /// of these is the default; <see cref="Enabled"/> lets an individual model be turned off without deleting it.
    /// </summary>
    [Table("provider_models")]
    public class ProviderModelEntity : AbstractDatabaseLogModel
    {
        [RequiredField]
        [MaxLengthField(100)]
        public string ProviderName { get; set; } = "";

        /// <summary>Model identifier as sent to the provider API (e.g. "gpt-4o", "claude-sonnet-4-6").</summary>
        [RequiredField]
        [MaxLengthField(200)]
        public string ModelId { get; set; } = "";

        [MaxLengthField(200)]
        public string? DisplayName { get; set; }

        public bool Enabled { get; set; } = true;

        public int SortOrder { get; set; } = 0;

        /// <summary>Whether this model supports a reasoning / "deep thinking" mode.</summary>
        public bool Reasoning { get; set; }

        /// <summary>Context window in tokens; 0 = unspecified.</summary>
        public int ContextWindow { get; set; }

        /// <summary>Accepted input modalities as a CSV (e.g. <c>text,image</c>); null = text only.</summary>
        [MaxLengthField(100)]
        public string? InputModalities { get; set; }

        public override AbstractModel CopyTo(AbstractModel? clone = null)
        {
            var target = clone as ProviderModelEntity ?? new ProviderModelEntity();
            base.CopyTo(target);
            target.ProviderName = ProviderName;
            target.ModelId = ModelId;
            target.DisplayName = DisplayName;
            target.Enabled = Enabled;
            target.SortOrder = SortOrder;
            target.Reasoning = Reasoning;
            target.ContextWindow = ContextWindow;
            target.InputModalities = InputModalities;
            return target;
        }

        public override void LoadFrom(IGuidEntity data)
        {
            base.LoadFrom(data);
        }
    }
}
