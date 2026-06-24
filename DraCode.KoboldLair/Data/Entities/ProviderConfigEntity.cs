using Birko.Data.Models;
using Birko.Data.SQL.Attributes;

namespace DraCode.KoboldLair.Data.Entities
{
    /// <summary>
    /// Database entity for an LLM provider's configuration (TASK-073). Replaces the appsettings
    /// <c>Providers</c> list + the per-provider API-key-in-env. The API key is stored ENCRYPTED in
    /// <see cref="ApiKeyCiphertext"/> (AES via Birko.Security) — never plaintext. The provider's
    /// switchable models live in separate <see cref="ProviderModelEntity"/> rows; agent→provider
    /// selections live in <see cref="AgentProviderSettingEntity"/> rows.
    /// </summary>
    [Table("provider_configs")]
    public class ProviderConfigEntity : AbstractDatabaseLogModel
    {
        /// <summary>Unique provider id (e.g. "openai", "claude", "ollama").</summary>
        [RequiredField]
        [MaxLengthField(100)]
        public string Name { get; set; } = "";

        [MaxLengthField(200)]
        public string? DisplayName { get; set; }

        /// <summary>Provider type used by the agent factory (often equals <see cref="Name"/>).</summary>
        [RequiredField]
        [MaxLengthField(50)]
        public string Type { get; set; } = "";

        public bool Enabled { get; set; } = true;

        [MaxLengthField(500)]
        public string? BaseUrl { get; set; }

        public bool RequiresApiKey { get; set; } = true;

        /// <summary>Default model id; should match one enabled <see cref="ProviderModelEntity.ModelId"/>.</summary>
        [MaxLengthField(200)]
        public string? DefaultModel { get; set; }

        /// <summary>JSON array of agent types this provider serves (e.g. <c>["all"]</c> or <c>["kobold","dragon"]</c>).</summary>
        public string CompatibleAgentsJson { get; set; } = "[]";

        /// <summary>JSON dict of NON-secret provider config (endpoints, flags). The API key is NOT stored here.</summary>
        public string ConfigurationJson { get; set; } = "{}";

        /// <summary>AES-encrypted API key (ciphertext). Null when the provider needs no key / none set yet.</summary>
        public string? ApiKeyCiphertext { get; set; }

        public override AbstractModel CopyTo(AbstractModel? clone = null)
        {
            var target = clone as ProviderConfigEntity ?? new ProviderConfigEntity();
            base.CopyTo(target);
            target.Name = Name;
            target.DisplayName = DisplayName;
            target.Type = Type;
            target.Enabled = Enabled;
            target.BaseUrl = BaseUrl;
            target.RequiresApiKey = RequiresApiKey;
            target.DefaultModel = DefaultModel;
            target.CompatibleAgentsJson = CompatibleAgentsJson;
            target.ConfigurationJson = ConfigurationJson;
            target.ApiKeyCiphertext = ApiKeyCiphertext;
            return target;
        }

        public override void LoadFrom(IGuidEntity data)
        {
            base.LoadFrom(data);
        }
    }
}
