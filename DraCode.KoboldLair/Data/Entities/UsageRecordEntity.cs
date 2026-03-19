using Birko.Data.Models;
using Birko.Data.SQL.Attributes;
using Birko.Data.ViewModels;

namespace DraCode.KoboldLair.Data.Entities
{
    /// <summary>
    /// Database entity for LLM API usage records.
    /// Tracks token usage and estimated cost per API call.
    /// </summary>
    [Table("usage_records")]
    public class UsageRecordEntity : AbstractDatabaseLogModel
    {
        [RequiredField]
        [MaxLengthField(50)]
        public string Provider { get; set; } = "";

        [MaxLengthField(100)]
        public string Model { get; set; } = "";

        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }

        /// <summary>
        /// Estimated cost in USD based on configured pricing
        /// </summary>
        public double EstimatedCostUsd { get; set; }

        [MaxLengthField(36)]
        public string? ProjectId { get; set; }

        [MaxLengthField(36)]
        public string? TaskId { get; set; }

        [MaxLengthField(50)]
        public string? AgentType { get; set; }

        [MaxLengthField(100)]
        public string? CallerContext { get; set; }

        public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

        public override AbstractModel CopyTo(AbstractModel? clone = null)
        {
            var target = clone as UsageRecordEntity ?? new UsageRecordEntity();
            base.CopyTo(target);
            target.Provider = Provider;
            target.Model = Model;
            target.PromptTokens = PromptTokens;
            target.CompletionTokens = CompletionTokens;
            target.TotalTokens = TotalTokens;
            target.EstimatedCostUsd = EstimatedCostUsd;
            target.ProjectId = ProjectId;
            target.TaskId = TaskId;
            target.AgentType = AgentType;
            target.CallerContext = CallerContext;
            target.RecordedAt = RecordedAt;
            return target;
        }

        public override void LoadFrom(IGuidEntity data)
        {
            base.LoadFrom(data);
            if (data is UsageRecordViewModel vm)
            {
                Provider = vm.Provider;
                Model = vm.Model;
                PromptTokens = vm.PromptTokens;
                CompletionTokens = vm.CompletionTokens;
                TotalTokens = vm.TotalTokens;
                EstimatedCostUsd = vm.EstimatedCostUsd;
                ProjectId = vm.ProjectId;
                TaskId = vm.TaskId;
                AgentType = vm.AgentType;
                CallerContext = vm.CallerContext;
                RecordedAt = vm.RecordedAt;
            }
        }
    }

    public class UsageRecordViewModel : LogViewModel
    {
        public string Provider { get; set; } = "";
        public string Model { get; set; } = "";
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
        public double EstimatedCostUsd { get; set; }
        public string? ProjectId { get; set; }
        public string? TaskId { get; set; }
        public string? AgentType { get; set; }
        public string? CallerContext { get; set; }
        public DateTime RecordedAt { get; set; }

        public void LoadFrom(UsageRecordEntity data)
        {
            base.LoadFrom((AbstractModel)data);
            if (data != null)
            {
                Provider = data.Provider;
                Model = data.Model;
                PromptTokens = data.PromptTokens;
                CompletionTokens = data.CompletionTokens;
                TotalTokens = data.TotalTokens;
                EstimatedCostUsd = data.EstimatedCostUsd;
                ProjectId = data.ProjectId;
                TaskId = data.TaskId;
                AgentType = data.AgentType;
                CallerContext = data.CallerContext;
                RecordedAt = data.RecordedAt;
            }
        }
    }
}
