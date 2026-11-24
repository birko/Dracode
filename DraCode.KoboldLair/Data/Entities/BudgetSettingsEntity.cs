using Birko.Data.Models;
using Birko.Data.SQL.Attributes;

namespace DraCode.KoboldLair.Data.Entities
{
    /// <summary>
    /// The LLM spending budgets an admin set in Settings → Budgets (TASK-163): one row, which wins over
    /// <c>KoboldLair:CostTracking:Budget</c> in appsettings. A value of 0 means no budget of that kind.
    /// </summary>
    [Table("budget_settings")]
    public class BudgetSettingsEntity : AbstractDatabaseLogModel
    {
        public double DailyBudgetUsd { get; set; }
        public double MonthlyBudgetUsd { get; set; }
        public double ProjectBudgetUsd { get; set; }
        public double WarningThresholdPercent { get; set; } = 80;

        /// <summary>Who made the last change: the caller's subject.</summary>
        [MaxLengthField(200)]
        public string? UpdatedBy { get; set; }

        public override AbstractModel CopyTo(AbstractModel? clone = null)
        {
            var target = clone as BudgetSettingsEntity ?? new BudgetSettingsEntity();
            base.CopyTo(target);
            target.DailyBudgetUsd = DailyBudgetUsd;
            target.MonthlyBudgetUsd = MonthlyBudgetUsd;
            target.ProjectBudgetUsd = ProjectBudgetUsd;
            target.WarningThresholdPercent = WarningThresholdPercent;
            target.UpdatedBy = UpdatedBy;
            return target;
        }

        public override void LoadFrom(IGuidEntity data)
        {
            base.LoadFrom(data);
        }
    }
}
