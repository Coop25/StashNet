using System;
using System.Collections.Generic;
using StashNet.Blocks;
using StashNet.Core.Drives;
using StashNet.Core.Network;
using StashNet.Drives;

namespace StashNet.Network
{
    /// <summary>
    /// A snapshot of one storage network: its Core, and every readable drive in its bays.
    /// Opened fresh for every operation, so it is never stale.
    /// </summary>
    public sealed class StashNetNetwork
    {
        public const string PropRadius = "StashNetRadius";

        private sealed class Slot
        {
            public TEFeatureStorage Bay;
            public int Index;
        }

        private static int maxCoreRadius = -1;

        private StashNetNetwork(Vector3i corePos, NetworkStorage storage, int bayCount, int skippedBays, int skippedDrives)
        {
            CorePos = corePos;
            Storage = storage;
            BayCount = bayCount;
            SkippedBays = skippedBays;
            SkippedDrives = skippedDrives;
        }

        public Vector3i CorePos { get; }
        public NetworkStorage Storage { get; }
        public int BayCount { get; }
        /// <summary>Bays skipped because someone has them open.</summary>
        public int SkippedBays { get; }
        /// <summary>Drives skipped because their data can't be read (left untouched).</summary>
        public int SkippedDrives { get; }

        public static bool IsCore(Block block) => block != null && block.Properties.Contains(PropRadius);

        public static bool TryOpen(World world, Vector3i terminalPos, out StashNetNetwork network, out string problem)
        {
            network = null;
            problem = null;

            if (!TryFindCore(world, terminalPos, out Vector3i corePos, out int radius))
            {
                problem = Localization.Get("stashnetNoCore");
                return false;
            }

            var drives = new List<NetworkDrive>();
            int bays = 0, skippedBays = 0, skippedDrives = 0;
            foreach (Vector3i pos in Cube(corePos, radius))
            {
                BlockValue bv = world.GetBlock(pos);
                if (bv.ischild || !(bv.Block is BlockCompositeTileEntity)) continue;
                TileEntityComposite te = world.GetTileEntity(pos) as TileEntityComposite;
                if (te?.GetFeature<TEFeatureStashNetDriveBay>() == null) continue;
                TEFeatureStorage storage = te.GetFeature<TEFeatureStorage>();
                if (storage == null) continue;

                bays++;
                if (storage.IsUserAccessing())
                {
                    skippedBays++;
                    continue;
                }
                for (int i = 0; i < storage.ItemGrid.Length; i++)
                {
                    ItemValue drive = storage.ItemGrid[i].itemValue;
                    if (drive == null || drive.IsEmpty() || !DriveRules.IsDrive(drive)) continue;
                    if (!DriveItem.TryGetCapacity(drive, out DriveCapacity cap) || !DriveItem.TryLoad(drive, out DriveContents contents, out _))
                    {
                        skippedDrives++;
                        continue;
                    }
                    drives.Add(new NetworkDrive(contents, cap, new Slot { Bay = storage, Index = i }));
                }
            }

            network = new StashNetNetwork(corePos, new NetworkStorage(drives), bays, skippedBays, skippedDrives);
            return true;
        }

        /// <summary>Writes every changed drive back into its bay slot.</summary>
        public void SaveDirty()
        {
            foreach (NetworkDrive d in Storage.Drives)
            {
                if (!d.Dirty) continue;
                var slot = (Slot)d.Tag;
                ItemStack current = slot.Bay.ItemGrid[slot.Index];
                ItemValue updated = current.itemValue.Clone();
                DriveItem.Save(updated, d.Contents);
                slot.Bay.UpdateSlot(slot.Index, new ItemStack(updated, current.count));
                slot.Bay.SetModified();
            }
        }

        /// <summary>Nearest Core (cube distance) whose own radius reaches the terminal.</summary>
        private static bool TryFindCore(World world, Vector3i from, out Vector3i corePos, out int coreRadius)
        {
            corePos = default;
            coreRadius = 0;
            int best = int.MaxValue;
            foreach (Vector3i pos in Cube(from, MaxCoreRadius()))
            {
                BlockValue bv = world.GetBlock(pos);
                if (bv.ischild || !IsCore(bv.Block)) continue;
                int radius = bv.Block.Properties.GetInt(PropRadius);
                int dist = CubeDistance(pos, from);
                if (dist > radius || dist >= best) continue;
                best = dist;
                corePos = pos;
                coreRadius = radius;
            }
            return best != int.MaxValue;
        }

        private static int MaxCoreRadius()
        {
            if (maxCoreRadius >= 0) return maxCoreRadius;
            int max = 0;
            foreach (Block b in Block.list)
            {
                if (IsCore(b)) max = Math.Max(max, b.Properties.GetInt(PropRadius));
            }
            maxCoreRadius = max;
            return max;
        }

        private static int CubeDistance(Vector3i a, Vector3i b) =>
            Math.Max(Math.Abs(a.x - b.x), Math.Max(Math.Abs(a.y - b.y), Math.Abs(a.z - b.z)));

        private static IEnumerable<Vector3i> Cube(Vector3i center, int r)
        {
            for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                    for (int z = -r; z <= r; z++)
                        yield return new Vector3i(center.x + x, center.y + y, center.z + z);
        }
    }
}
