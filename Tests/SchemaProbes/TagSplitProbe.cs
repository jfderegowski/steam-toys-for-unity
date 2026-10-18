using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SteamToys.Runtime.InventorySystem;
using Steamworks;
using UnityEditor;
using UnityEngine;

namespace SteamToys.Tests.SchemaProbes
{
    /// <summary>
    /// Checks how Steam reports the tags of an item made with tag generators: whether the <c>tags</c> of the item
    /// hold only what was generated while its definition keeps its own, what a generator's own tags add, which tag
    /// wins when a tag generator sets a category the definition also has, and which of the two an exchange recipe
    /// matches. Upload <c>Logs/SteamSchemaProbes/5-tag-split.json</c> to the probe app first; the report goes next to it.
    /// It reads Steam directly, not through <see cref="Inventory"/>, so the report shows what Steam sends.
    /// </summary>
    internal static class TagSplitProbe
    {
        private const string REPORT_PATH = "Logs/SteamSchemaProbes/5-tag-split-report.md";
        private const uint PROPERTY_BUFFER_SIZE = 8192;
        private const int TIMEOUT_MS = 30000;

        private const int ITEM = 92001;
        private const int GENERATOR = 92010;
        private const int BUNDLE = 92011;
        private const int TAGGED_GENERATOR = 92012;
        private const int COLLISION_GENERATOR = 92013;
        private const int EXCHANGE_GENERATED = 92020;
        private const int EXCHANGE_COLLISION_GENERATED = 92021;
        private const int EXCHANGE_DEFINITION = 92022;

        private static readonly Dictionary<SteamInventoryResult_t, TaskCompletionSource<EResult>> Pending = new();

        private static Callback<SteamInventoryResultReady_t> _resultReady;

        private readonly struct CreatedItem
        {
            public readonly int ItemDefId;
            public readonly SteamItemInstanceID_t InstanceId;
            public readonly string Tags;

            public CreatedItem(int itemDefId, SteamItemInstanceID_t instanceId, string tags)
            {
                ItemDefId = itemDefId;
                InstanceId = instanceId;
                Tags = tags;
            }

            public override string ToString() => $"{ItemDefId}#{InstanceId.m_SteamItemInstanceID} tags=\"{Tags}\"";
        }

        [MenuItem("Tools/Steam Toys/Probes/Tag Split")]
        private static async void Run()
        {
            try
            {
                await RunAsync();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static async Task RunAsync()
        {
            if (!Inventory.Initialized)
            {
                Debug.LogError("[TagSplitProbe] Steam is not running in the editor; start the session first.");
                return;
            }

            _resultReady ??= Callback<SteamInventoryResultReady_t>.Create(OnResultReady);
            SteamInventory.LoadItemDefinitions();

            var report = new StringBuilder();
            report.AppendLine("# Tag split probe").AppendLine();
            report.AppendLine($"Run {DateTime.Now:yyyy-MM-dd HH:mm:ss}.").AppendLine();

            report.AppendLine("## Definitions on the client").AppendLine();
            report.AppendLine("| itemdefid | name | tags | tag_generators |").AppendLine("|---|---|---|---|");

            foreach (var id in new[] { ITEM, GENERATOR, BUNDLE, TAGGED_GENERATOR, COLLISION_GENERATOR })
                report.AppendLine($"| {id} | {ReadDefinition(id, "name")} | `{ReadDefinition(id, "tags")}` | `{ReadDefinition(id, "tag_generators")}` |");

            if (string.IsNullOrEmpty(ReadDefinition(ITEM, "name")))
                report.AppendLine().AppendLine("**The client does not have the definitions yet.** Restart Steam or wait, then run again.");

            report.AppendLine().AppendLine("## Granted").AppendLine();
            report.AppendLine("| case | granted | created | instance tags | expected |").AppendLine("|---|---|---|---|---|");

            var fromGenerator = await Grant(report, "generator with a tag generator", GENERATOR,
                "probe_split_gen:a only, no probe_split_def");
            await Grant(report, "bundle with a tag generator", BUNDLE, "probe_split_gen:a only");
            await Grant(report, "generator with its own tags and a tag generator", TAGGED_GENERATOR,
                "probe_split_src:yes and probe_split_gen:a?");
            var direct = await Grant(report, "the item itself", ITEM, "empty");
            var collisionA = await Grant(report, "tag generator on a category the item has", COLLISION_GENERATOR,
                "probe_split_def:gen?");
            var collisionB = await Grant(report, "the same again, for the second exchange", COLLISION_GENERATOR,
                "probe_split_def:gen?");

            report.AppendLine().AppendLine("## Exchanged").AppendLine();
            report.AppendLine("| case | recipe | material | result |").AppendLine("|---|---|---|---|");

            await Exchange(report, "recipe asks for a generated tag", EXCHANGE_GENERATED, "probe_split_gen:a", fromGenerator);
            await Exchange(report, "recipe asks for a definition tag (control)", EXCHANGE_DEFINITION, "probe_split_def:yes", direct);
            await Exchange(report, "recipe asks for the generated value of a collision", EXCHANGE_COLLISION_GENERATED,
                "probe_split_def:gen", collisionA);
            await Exchange(report, "recipe asks for the definition value of a collision", EXCHANGE_DEFINITION,
                "probe_split_def:yes", collisionB);

            Directory.CreateDirectory(Path.GetDirectoryName(REPORT_PATH)!);
            File.WriteAllText(REPORT_PATH, report.ToString());

            Debug.Log($"[TagSplitProbe] Done, report in {REPORT_PATH}\n{report}");
        }

        #region Steps

        private static async Task<CreatedItem?> Grant(StringBuilder report, string name, int itemDefId, string expected)
        {
            var started = SteamInventory.GenerateItems(out var handle,
                new[] { new SteamItemDef_t(itemDefId) }, new uint[] { 1 }, 1);

            var (result, items) = await Wait(started, handle);
            var created = items.FirstOrDefault();

            report.AppendLine($"| {name} | {itemDefId} | {(items.Count == 0 ? result.ToString() : string.Join("<br>", items.Select(item => $"{item.ItemDefId}#{item.InstanceId.m_SteamItemInstanceID}")))} " +
                              $"| `{string.Join("` `", items.Select(item => item.Tags))}` | {expected} |");

            return items.Count == 0 ? null : created;
        }

        private static async Task Exchange(StringBuilder report, string name, int target, string recipe, CreatedItem? material)
        {
            if (material == null)
            {
                report.AppendLine($"| {name} | `{recipe}` | none, its grant failed | skipped |");
                return;
            }

            var started = SteamInventory.ExchangeItems(out var handle,
                new[] { new SteamItemDef_t(target) }, new uint[] { 1 }, 1,
                new[] { material.Value.InstanceId }, new uint[] { 1 }, 1);

            var (result, items) = await Wait(started, handle);
            var created = items.Where(item => item.ItemDefId == target).ToList();

            report.AppendLine($"| {name} | `{recipe}` | {material.Value} | " +
                              $"{(result == EResult.k_EResultOK && created.Count > 0 ? "matched, made " + created[0].ItemDefId : "no match: " + result)} |");
        }

        #endregion

        #region Steam

        private static async Task<(EResult result, List<CreatedItem> items)> Wait(bool started, SteamInventoryResult_t handle)
        {
            var items = new List<CreatedItem>();

            if (!started)
                return (EResult.k_EResultFail, items);

            var completion = new TaskCompletionSource<EResult>();
            Pending[handle] = completion;

            if (await Task.WhenAny(completion.Task, Task.Delay(TIMEOUT_MS)) != completion.Task)
            {
                Pending.Remove(handle);
                SteamInventory.DestroyResult(handle);
                return (EResult.k_EResultTimeout, items);
            }

            var result = completion.Task.Result;

            try
            {
                if (result == EResult.k_EResultOK)
                    items.AddRange(ReadItems(handle));
            }
            finally
            {
                SteamInventory.DestroyResult(handle);
            }

            return (result, items);
        }

        private static void OnResultReady(SteamInventoryResultReady_t callback)
        {
            if (Pending.Remove(callback.m_handle, out var completion))
                completion.SetResult(callback.m_result);
        }

        private static IEnumerable<CreatedItem> ReadItems(SteamInventoryResult_t handle)
        {
            uint count = 0;

            if (!SteamInventory.GetResultItems(handle, null, ref count) || count == 0)
                yield break;

            var details = new SteamItemDetails_t[count];

            if (!SteamInventory.GetResultItems(handle, details, ref count))
                yield break;

            for (uint i = 0; i < count; i++)
            {
                // A consumed material comes back marked as removed; only the made items count here.
                if ((details[i].m_unFlags & (ushort)ESteamItemFlags.k_ESteamItemRemoved) != 0)
                    continue;

                var tags = ReadProperty((string name, ref uint size, out string value) =>
                    SteamInventory.GetResultItemProperty(handle, i, name, out value, ref size), "tags");

                yield return new CreatedItem(details[i].m_iDefinition.m_SteamItemDef, details[i].m_itemId, tags);
            }
        }

        private static string ReadDefinition(int itemDefId, string property) =>
            ReadProperty((string name, ref uint size, out string value) =>
                SteamInventory.GetItemDefinitionProperty(new SteamItemDef_t(itemDefId), name, out value, ref size), property);

        private delegate bool PropertyReader(string name, ref uint size, out string value);

        private static string ReadProperty(PropertyReader reader, string name)
        {
            var size = PROPERTY_BUFFER_SIZE;

            return reader(name, ref size, out var value) ? value ?? string.Empty : string.Empty;
        }

        #endregion
    }
}
