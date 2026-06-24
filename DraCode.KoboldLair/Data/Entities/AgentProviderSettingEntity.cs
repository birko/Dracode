using Birko.Data.Models;
using Birko.Data.SQL.Attributes;

namespace DraCode.KoboldLair.Data.Entities
{
    /// <summary>
    /// One agent→provider/model assignment (TASK-073) — the DB form of what <c>user-settings.json</c>
    /// held. <see cref="AgentKey"/> is the orchestrator role (<c>dragon</c>/<c>wyvern</c>/<c>wyrm</c>/<c>kobold</c>)
    /// or a per-Kobold-type override keyed <c>kobold:&lt;agentType&gt;</c> (e.g. <c>kobold:csharp</c>). A null
    /// <see cref="Provider"/>/<see cref="Model"/> means "fall back to the system default".
    /// </summary>
    [Table("agent_provider_settings")]
    public class AgentProviderSettingEntity : AbstractDatabaseLogModel
    {
        [RequiredField]
        [MaxLengthField(100)]
        public string AgentKey { get; set; } = "";

        [MaxLengthField(100)]
        public string? Provider { get; set; }

        [MaxLengthField(200)]
        public string? Model { get; set; }

        public override AbstractModel CopyTo(AbstractModel? clone = null)
        {
            var target = clone as AgentProviderSettingEntity ?? new AgentProviderSettingEntity();
            base.CopyTo(target);
            target.AgentKey = AgentKey;
            target.Provider = Provider;
            target.Model = Model;
            return target;
        }

        public override void LoadFrom(IGuidEntity data)
        {
            base.LoadFrom(data);
        }
    }
}
