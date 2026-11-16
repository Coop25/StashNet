using StashNet.Core.Heat;
using StashNet.Network;

namespace StashNet.Heat
{
    /// <summary>
    /// Milestone 7: feeds the game's own heat system (the one forges and campfires feed, which
    /// brings screamers). Everything goes through AIDirector.NotifyActivity, which already ignores
    /// heat during blood moons, when the heat setting is off, and applies heat sensitivity.
    /// </summary>
    public static class HeatEmitter
    {
        public const string PropHeatPerDrive = "StashNetHeatPerDrive";
        public const string PropHeatCap = "StashNetHeatCap";

        public static bool IsServer => SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer;

        /// <summary>SERVER ONLY. Reports heat at a position; no-op for 0 heat or without an AI director.</summary>
        public static void Emit(World world, Vector3i pos, float value)
        {
            if (value <= 0f || world?.aiDirector == null || !IsServer) return;
            world.aiDirector.NotifyActivity(EnumAIDirectorChunkEvent.Campfire, pos, value);
        }

        /// <summary>
        /// Heat one report of this Core's network is worth, from the shared live summary (no drive reads). Counts every drive in its bays, readable
        /// or not, by heat weight (bigger drives run hotter, M8). Drives in a bay someone has open are
        /// skipped for this one report (acceptable).
        /// </summary>
        public static float CoreHeat(World world, Vector3i corePos, out int drives, out float weight)
        {
            drives = 0;
            weight = 0f;
            Block core = world.GetBlock(corePos).Block;
            float perDrive = GetFloat(core, PropHeatPerDrive);
            float cap = GetFloat(core, PropHeatCap);
            if (perDrive <= 0f || cap <= 0f) return 0f;
            // A Core finds itself at distance 0, so this opens exactly this Core's network.
            if (!NetworkSummaryCache.TryGet(world, corePos, out _, out Core.Network.NetworkSummary net)) return 0f;
            drives = net.DriveCount;
            weight = net.HeatWeight;
            return NetworkHeat.PerReport(weight, perDrive, cap);
        }

        public const string PropHeatOnUse = "StashNetHeatOnUse";
        private static readonly System.Collections.Generic.Dictionary<Vector3i, ulong> terminalNext =
            new System.Collections.Generic.Dictionary<Vector3i, ulong>();

        /// <summary>
        /// SERVER ONLY. A Storage Terminal request was handled: report a small bump at the terminal,
        /// at most once per vanilla heat interval per terminal. The Crafting Terminal is skipped
        /// (it already gives off workbench heat while crafting).
        /// </summary>
        public static void TerminalUsed(World world, Vector3i terminalPos)
        {
            Block block = world.GetBlock(terminalPos).Block;
            if (!(block is Blocks.BlockStashNetTerminal)) return;
            float value = GetFloat(block, PropHeatOnUse);
            if (value <= 0f) return;
            if (terminalNext.Count > 256) terminalNext.Clear();
            terminalNext.TryGetValue(terminalPos, out ulong next);
            if (!HeatThrottle.ShouldEmit(world.worldTime, ref next, AIDirector.GetActivityWorldTimeDelay())) return;
            terminalNext[terminalPos] = next;
            Emit(world, terminalPos, value);
        }

        internal static float GetFloat(Block block, string prop) =>
            block != null && block.Properties.Contains(prop) ? block.Properties.GetFloat(prop) : 0f;
    }
}
