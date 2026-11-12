using System.Collections.Generic;
using System.Text;

namespace StashNet.Dev
{
    /// <summary>
    /// Developer console command (F1 in game). Milestone 0 uses it to check that a drive-sized
    /// blob of data survives being saved on an item.
    ///   stashnet info            - print the mod version
    ///   stashnet metatest [n]    - write a fake drive with n item types onto the held item (default 100)
    ///   stashnet metacheck       - verify the held item's fake drive is intact
    ///   stashnet metaclear       - remove the test data from the held item
    /// </summary>
    public class ConsoleCmdStashNet : ConsoleCmdAbstract
    {
        private const string PayloadKey = "StashNetTestPayload";
        private const string CountKey = "StashNetTestCount";

        public override bool IsExecuteOnClient => true;

        public override string[] getCommands() => new[] { "stashnet", "sn" };

        public override string getDescription() => "StashNet developer tools (info, metatest, metacheck, metaclear).";

        public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
        {
            string sub = _params.Count > 0 ? _params[0].ToLowerInvariant() : "info";
            switch (sub)
            {
                case "info":
                    Out($"StashNet v{StashNetMod.Version} is loaded.");
                    return;
                case "metatest":
                    int n = 100;
                    if (_params.Count > 1 && (!int.TryParse(_params[1], out n) || n < 1 || n > 5000))
                    {
                        Out("Usage: stashnet metatest [1-5000]");
                        return;
                    }
                    MetaTest(n);
                    return;
                case "metacheck":
                    MetaCheck();
                    return;
                case "metaclear":
                    MetaClear();
                    return;
                default:
                    Out("Unknown subcommand. Try: info, metatest [n], metacheck, metaclear");
                    return;
            }
        }

        /// <summary>A realistic-looking drive payload: one entry per item type.</summary>
        internal static string BuildPayload(int types)
        {
            var sb = new StringBuilder(types * 48);
            for (int i = 0; i < types; i++)
            {
                sb.Append("stashnet_test_item_").Append(i.ToString("D4"))
                  .Append('|').Append(1000 + i)      // count
                  .Append('|').Append(i % 7)         // quality
                  .Append('|').Append(i * 3 % 1000)  // durability
                  .Append(';');
            }
            return sb.ToString();
        }

        private static void MetaTest(int types)
        {
            if (!TryGetHeld(out var player, out var itemValue)) return;
            string payload = BuildPayload(types);
            itemValue.SetMetadata(PayloadKey, payload);
            itemValue.SetMetadata(CountKey, types);
            player.inventory.SetItem(player.inventory.holdingItemIdx, itemValue, player.inventory.holdingCount);
            Out($"Wrote fake drive: {types} item types, {Encoding.UTF8.GetByteCount(payload):N0} bytes, onto '{itemValue.ItemClass.GetItemName()}'.");
            Out("Now save & quit to the main menu, reload the world, hold the same item and run: stashnet metacheck");
        }

        private static void MetaCheck()
        {
            if (!TryGetHeld(out _, out var itemValue)) return;
            if (!itemValue.TryGetMetadata(CountKey, out int types) || !itemValue.TryGetMetadata(PayloadKey, out string payload))
            {
                Out("FAIL: no test data on the held item (it was lost, or metatest was never run on it).");
                return;
            }
            Out(payload == BuildPayload(types)
                ? $"PASS: fake drive intact - {types} item types, {Encoding.UTF8.GetByteCount(payload):N0} bytes."
                : $"FAIL: test data is present but corrupted (expected {types} types).");
        }

        private static void MetaClear()
        {
            if (!TryGetHeld(out var player, out var itemValue)) return;
            itemValue.RemoveMetaData(PayloadKey);
            itemValue.RemoveMetaData(CountKey);
            player.inventory.SetItem(player.inventory.holdingItemIdx, itemValue, player.inventory.holdingCount);
            Out("Test data removed from the held item.");
        }

        private static bool TryGetHeld(out EntityPlayerLocal player, out ItemValue itemValue)
        {
            player = GameManager.Instance?.World?.GetPrimaryPlayer();
            itemValue = player?.inventory.holdingItemItemValue;
            if (player == null || itemValue == null || itemValue.IsEmpty())
            {
                Out("Hold an item in your hand first (any tool or weapon works).");
                return false;
            }
            return true;
        }

        private static void Out(string line) => SingletonMonoBehaviour<SdtdConsole>.Instance.Output("[StashNet] " + line);
    }
}
