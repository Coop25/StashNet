using System;
using StashNet.Network;

namespace StashNet.Heat
{
    /// <summary>`stashnet heat`: shows the nearest network's heat and the heat level where you stand (M7).</summary>
    internal static class HeatCommand
    {
        /// <summary>The game's line for a screamer roll (AIDirectorChunkEventComponent.cActivityLevelToSpawn).</summary>
        private const float ScreamerLine = 25f;

        public static void Run(Action<string> output)
        {
            World world = GameManager.Instance?.World;
            EntityPlayerLocal player = world?.GetPrimaryPlayer();
            if (player == null)
            {
                output("No local player.");
                return;
            }
            if (!HeatEmitter.IsServer || world.aiDirector == null)
            {
                output("Heat is tracked on the server/host only.");
                return;
            }

            Vector3i here = World.worldToBlockPos(player.position);
            if (StashNetNetwork.TryFindCore(world, here, out Vector3i corePos, out _))
            {
                Block core = world.GetBlock(corePos).Block;
                float heat = HeatEmitter.CoreHeat(world, corePos, out int drives, out float weight);
                output($"Core at {corePos}: {drives} drive(s), heat weight {weight:0.#} -> {heat:0.##} heat per report " +
                       $"({HeatEmitter.GetFloat(core, HeatEmitter.PropHeatPerDrive):0.##} per weight, cap {HeatEmitter.GetFloat(core, HeatEmitter.PropHeatCap):0.##}), " +
                       "about every 3 minutes.");
            }
            else
            {
                output("No StashNet Core in range.");
            }

            float area = world.aiDirector.chunkEventComponent?.GetChunkDataFromPosition(here, false)?.ActivityLevel ?? 0f;
            output($"Heat in this area (all sources): {area:0.#} / {ScreamerLine:0} (at {ScreamerLine:0} the game rolls a 20% screamer chance). " +
                   $"Heat sensitivity: x{AIDirector.HeatMapSensitivityModifier:0.##}. Heat is paused during blood moons.");
        }
    }
}
