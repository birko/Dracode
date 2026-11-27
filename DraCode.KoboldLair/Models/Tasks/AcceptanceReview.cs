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

        /// <summary>
        /// Whether a review card is an escalation (TASK-222, FEATURE-078 D9, D29): a task whose checks failed past the
        /// reopen limit. It carries the findings on its escalation and also offers "Handle it automatically".
        /// </summary>
        public static bool IsEscalation(PendingDecision decision) =>
            decision.Source == PendingDecisionSource.Approval && decision.Escalation != null;

        /// <summary>
        /// The escalation card's question: the task, the findings of each earlier attempt (its reopen notes) and of this
        /// one, and the three answers.
        /// </summary>
        public static string EscalationQuestion(TaskRecord task, IReadOnlyList<string> findings)
        {
            var text = new StringBuilder();
            var notes = task.Task.Split(TaskAcceptanceService.ReopenNotePrefix);
            var title = notes[0].Split('\n')[0];
            text.Append($"The task \"{(title.Length > 160 ? title[..160] + "…" : title)}\" failed its checks ");
            text.Append($"{task.AcceptanceAttempts + 1} times and is no longer sent back on its own.");
            if (notes.Length > 1)
            {
                text.Append("\nEarlier attempts:");
                foreach (var note in notes.Skip(1).Select(n => n.Trim()))
                {
                    // A note reads " 1): findings" after the split; shown as "attempt 1: findings"
                    var close = note.IndexOf("):", StringComparison.Ordinal);
                    text.Append($"\n- attempt {(close > 0 ? note[..close] + ":" + note[(close + 2)..] : note)}");
                }
            }
            text.Append("\nThis time:");
            foreach (var finding in findings)
                text.Append($"\n- {finding}");
            text.Append($"\nAnswer {Approve} to accept it as it is, write a note to send it back once more with it, or " +
                $"\"{PendingDecisionNotification.HandleAutomatically}\" to fail it and file a fix task.");
            return text.ToString();
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
