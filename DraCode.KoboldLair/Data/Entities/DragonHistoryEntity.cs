using Birko.Data.Models;
using Birko.Data.SQL.Attributes;

namespace DraCode.KoboldLair.Data.Entities
{
    /// <summary>
    /// Database entity for Dragon conversation history per project.
    /// Stores the full message history as a JSON array for each project.
    /// Replaces fire-and-forget file I/O with atomic database writes.
    /// </summary>
    [Table("dragon_history")]
    public class DragonHistoryEntity : AbstractDatabaseLogModel
    {
        [RequiredField]
        [MaxLengthField(255)]
        public string ProjectFolder { get; set; } = "";

        /// <summary>
        /// Full message history serialized as JSON array
        /// </summary>
        public string MessagesJson { get; set; } = "[]";

        public int MessageCount { get; set; } = 0;

        public override AbstractModel CopyTo(AbstractModel? clone = null)
        {
            var target = clone as DragonHistoryEntity ?? new DragonHistoryEntity();
            base.CopyTo(target);
            target.ProjectFolder = ProjectFolder;
            target.MessagesJson = MessagesJson;
            target.MessageCount = MessageCount;
            return target;
        }

        public override void LoadFrom(IGuidEntity data)
        {
            base.LoadFrom(data);
        }
    }
}
