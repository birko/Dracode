using System.Text.Json.Nodes;

namespace DraCode.KoboldLair.Services
{
    /// <summary>
    /// Upgrades a plan stored before TASK-126 (DraCode's own plan types) to the JSON shape of Birko's
    /// <c>ImplementationPlan</c>, so it deserializes into <see cref="Models.Agents.KoboldImplementationPlan"/>.
    /// Statuses need nothing: they are stored as integers and Birko pinned the same values. What changed:
    /// <list type="bullet">
    /// <item>step <c>lastErrorMessage</c> → <c>errorMessage</c></item>
    /// <item>step <c>errorCategory</c>: a string (<c>"Transient"</c> / <c>"Permanent"</c> / <c>"Unknown"</c>) → Birko's
    /// typed category; <c>"Unknown"</c> → null (not classified). Left as a string it fails the whole plan's load.</item>
    /// <item>escalation <c>koboldId</c> (a Guid) → <c>workerId</c> (its string)</item>
    /// </list>
    /// Applied to every object in the document, so the computed <c>currentStep</c> copy the old serializer wrote is
    /// upgraded too. A plan already in the new shape passes through unchanged.
    /// </summary>
    public static class LegacyPlanJson
    {
        /// <summary>Reads a stored plan of either shape. Every plan reader goes through this.</summary>
        public static Models.Agents.KoboldImplementationPlan? Deserialize(string json, System.Text.Json.JsonSerializerOptions options) =>
            System.Text.Json.JsonSerializer.Deserialize<Models.Agents.KoboldImplementationPlan>(Upgrade(json), options);

        public static string Upgrade(string json)
        {
            var root = JsonNode.Parse(json);
            if (root == null || !UpgradeNode(root)) return json;
            return root.ToJsonString();
        }

        // True when anything changed.
        private static bool UpgradeNode(JsonNode node)
        {
            var changed = false;
            switch (node)
            {
                case JsonObject obj:
                    changed |= Rename(obj, "lastErrorMessage", "errorMessage");
                    changed |= Rename(obj, "koboldId", "workerId");
                    changed |= UpgradeErrorCategory(obj);
                    foreach (var (_, child) in obj.ToList())
                        if (child != null) changed |= UpgradeNode(child);
                    break;
                case JsonArray array:
                    foreach (var child in array)
                        if (child != null) changed |= UpgradeNode(child);
                    break;
            }
            return changed;
        }

        // Keeps a value already under the new name; the legacy one only fills a missing or null one.
        private static bool Rename(JsonObject obj, string from, string to)
        {
            if (!TryGet(obj, from, out var key, out var value)) return false;
            obj.Remove(key);
            if (!TryGet(obj, to, out _, out var existing) || existing == null)
            {
                obj.Remove(to);
                obj[to] = value?.DeepClone();
            }
            return true;
        }

        private static bool UpgradeErrorCategory(JsonObject obj)
        {
            if (!TryGet(obj, "errorCategory", out var key, out var value)
                || value is not JsonValue v || !v.TryGetValue<string>(out var name))
                return false;

            obj[key] = name.Trim().ToLowerInvariant() switch
            {
                "transient" => (int)Birko.AI.Models.ErrorCategory.Transient,
                "permanent" => (int)Birko.AI.Models.ErrorCategory.Permanent,
                _ => null
            };
            return true;
        }

        // Stored plans are camelCase but the readers are case-insensitive, so match the key the same way.
        private static bool TryGet(JsonObject obj, string name, out string key, out JsonNode? value)
        {
            foreach (var (k, v) in obj)
            {
                if (string.Equals(k, name, StringComparison.OrdinalIgnoreCase))
                {
                    key = k;
                    value = v;
                    return true;
                }
            }
            key = name;
            value = null;
            return false;
        }
    }
}
