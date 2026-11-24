using Birko.Data.Models;
using Birko.Data.SQL.Attributes;

namespace DraCode.KoboldLair.Data.Entities
{
    /// <summary>
    /// A person's workspace layout in the web client (TASK-058): their current one (<see cref="IsCurrent"/>, restored on
    /// every load) or one they saved by name. The layout itself is the client's JSON (tab order, Dragon's width, the pin),
    /// kept as sent.
    /// </summary>
    [Table("user_layouts")]
    public class UserLayoutEntity : AbstractDatabaseLogModel
    {
        /// <summary>The owner: the caller's subject (<c>sub</c>), as projects store their owner.</summary>
        [MaxLengthField(200)]
        public string OwnerId { get; set; } = string.Empty;

        /// <summary>The name a person gave it; empty for their current layout.</summary>
        [MaxLengthField(100)]
        public string Name { get; set; } = string.Empty;

        public bool IsCurrent { get; set; }

        public string LayoutJson { get; set; } = "{}";

        public override AbstractModel CopyTo(AbstractModel? clone = null)
        {
            var target = clone as UserLayoutEntity ?? new UserLayoutEntity();
            base.CopyTo(target);
            target.OwnerId = OwnerId;
            target.Name = Name;
            target.IsCurrent = IsCurrent;
            target.LayoutJson = LayoutJson;
            return target;
        }

        public override void LoadFrom(IGuidEntity data)
        {
            base.LoadFrom(data);
        }
    }
}
