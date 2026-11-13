using System;
using StashNet.Core.Heat;

namespace StashNet.Heat
{
    /// <summary>
    /// Core feature (Mod/Config/blocks.xml, stashnetScrapCore). The game finds it by class name, so
    /// the name must not change and it needs a parameterless constructor. Saves nothing.
    ///
    /// Milestone 7: the game ticks every loaded tile entity; on the same rhythm as a forge
    /// (about every 167 real seconds) the server reports this network's heat at the Core.
    /// </summary>
    public class TEFeatureStashNetHeat : TEFeatureAbs
    {
        // In memory only: after loading, the first report comes on the first tick.
        private ulong nextHeatTime;

        public override void CopyFromInternal(TileEntityComposite _other)
        {
        }

        public override void UpdateTick(World _world)
        {
            base.UpdateTick(_world);
            if (!HeatEmitter.IsServer) return;
            try
            {
                if (!HeatThrottle.ShouldEmit(_world.worldTime, ref nextHeatTime, AIDirector.GetActivityWorldTimeDelay())) return;
                Vector3i pos = ToWorldPos();
                HeatEmitter.Emit(_world, pos, HeatEmitter.CoreHeat(_world, pos, out _, out _));
            }
            catch (Exception e)
            {
                Log.Warning($"[StashNet] heat: Core tick failed: {e.Message}");
            }
        }
    }
}
