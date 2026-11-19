using System.Text;
using DraCode.KoboldLair.Services;

namespace DraCode.KoboldLair.Models.Tasks
{
    /// <summary>
    /// A finished task's review card (TASK-025, FEATURE-031 D4): what the person is asked, and how an answer reads. An
    /// answer starting with "Approve" approves, anything after it a remark; anything else rejects, the text being the note
    /// the reopened task is given.
    /// </summary>
    public static class AcceptanceReview
    {
        public const string Approve = "Approve";
        public const string Reject = "Reject";

        public static bool IsApproval(string? answer) =>
            answer != null && answer.TrimStart().StartsWith(Approve, StringComparison.OrdinalIgnoreCase);

        /// <summary>The answer less its leading Approve / Reject and separators; empty for a bare choice.</summary>
        public static string Note(string? answer)
        {
            var text = (answer ?? "").Trim();
            foreach (var word in new[] { Approve, Reject })
            {
                if (text.StartsWith(word, StringComparison.OrdinalIgnoreCase))
                {
                    text = text[word.Length..].TrimStart(' ', ',', ':', ';', '-', '—', '.');
                    break;
                }
            }
            return text.Trim();
        }

        /// <summary>The card's question: the task, what was checked, and what a person must judge.</summary>
        public static string Question(TaskRecord task, AcceptanceResult result)
        {
            var text = new StringBuilder();
            var title = task.Task.Split('\n')[0];
            text.Append($"Review the finished task \"{(title.Length > 160 ? title[..160] + "…" : title)}\". ");
            text.Append("These could not be checked automatically:");
            foreach (var item in result.NeedsHuman)
                text.Append($"\n- {item}");
            if (result.Checked.Count > 0)
            {
                text.Append("\nChecked and held:");
                foreach (var item in result.Checked)
                    text.Append($"\n- {item}");
            }
            text.Append($"\nAnswer {Approve} (a remark may follow) or {Reject} with what is missing.");
            return text.ToString();
        }
    }
}
