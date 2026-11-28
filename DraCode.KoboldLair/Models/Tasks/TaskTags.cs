using System.Text.RegularExpressions;

namespace DraCode.KoboldLair.Models.Tasks
{
    /// <summary>
    /// A task's own tag: the <c>api-1</c> of a description starting "[api-1]", the name its dependents use. Only the start of
    /// a description counts (TASK-231): a tag quoted later in another task's text (a fix task naming the task it fixes, a
    /// reopen note's "[intent]") never makes that task the tagged one.
    /// </summary>
    public static partial class TaskTags
    {
        [GeneratedRegex(@"^\s*\[([^\]]+)\]")]
        private static partial Regex Leading();

        /// <summary>The tag a description starts with, or null.</summary>
        public static string? Of(string? text) => text != null && Leading().Match(text) is { Success: true } m ? m.Groups[1].Value : null;

        /// <summary>Whether <paramref name="text"/> is the task tagged <paramref name="tag"/> (any case).</summary>
        public static bool Is(string? text, string tag) => string.Equals(Of(text), tag, StringComparison.OrdinalIgnoreCase);
    }
}
