using System;
using System.Collections.Generic;
using StashNet.Blocks;
using StashNet.Core.Drives;
using StashNet.Core.Network;
using StashNet.Drives;

namespace StashNet.Network
{
    /// <summary>
    /// A snapshot of one storage network: its Core, its lock (which guards the bays and terminals,
    /// not the drives), and every readable drive in its bays.
    /// Opened fresh for every operation, so it is never stale. Only the server writes (SaveDirty).
    /// </summary>
    public sealed class StashNetNetwork
    {
        public const string PropRadius = "StashNetRadius";

        private sealed class Slot
        {
            public TEFeatureStorage Bay;
            public int Index;
            /// <summary>The drive still has a lock stamp from v0.5.0; remove it on save.</summary>
            public bool ClearStamp;
        }

        private static int maxCoreRadius = -1;

        private StashNetNetwork() { }

        public Vector3i CorePos { get; private set; }
        /// <summary>The Core's vanilla lock; null for Cores placed before Milestone 4 (= unlocked).</summary>
        public TEFeatureLockable CoreLock { get; private set; }
        public NetworkStorage Storage { get; private set; }
        public int BayCount { get; private set; }
        /// <summary>Bays skipped because someone has them open.</summary>
        public int SkippedBays { get; private set; }
        /// <summary>Drives skipped because their data can't be read (left untouched).</summary>
        public int SkippedDrives { get; private set; }

        public static bool IsCore(Block block) => block != null && block.Properties.Contains(PropRadius);

        /// <param name="problemKey">On failure, a Localization key describing why (translated on the client).</param>
        public static bool TryOpen(World world, Vector3i terminalPos, out StashNetNetwork network, out string problemKey)
        {
            network = null;
            problemKey = null;

            if (!TryFindCore(world, terminalPos, out Vector3i corePos, out int radius))
            {
                problemKey = "stashnetNoCore";
                return false;
            }

            var net = new StashNetNetwork { CorePos = corePos, CoreLock = NetworkLock.GetCoreLock(world, corePos) };

            var drives = new List<NetworkDrive>();
            foreach (Vector3i pos in Cube(corePos, radius))
            {
                BlockValue bv = world.GetBlock(pos);
                if (bv.ischild || !(bv.Block is BlockCompositeTileEntity)) continue;
                TileEntityComposite te = world.GetTileEntity(pos) as TileEntityComposite;
                if (te?.GetFeature<TEFeatureStashNetDriveBay>() == null) continue;
                TEFeatureStorage storage = te.GetFeature<TEFeatureStorage>();
                if (storage == null) continue;

                net.BayCount++;
                if (storage.IsUserAccessing())
                {
                    net.SkippedBays++;
                    continue;
                }
                for (int i = 0; i < storage.ItemGrid.Length; i++)
                {
                    ItemValue drive = storage.ItemGrid[i].itemValue;
                    if (drive == null || drive.IsEmpty() || !DriveRules.IsDrive(drive)) continue;

                    if (!DriveItem.TryGetCapacity(drive, out DriveCapacity cap) || !DriveItem.TryLoad(drive, out DriveContents contents, out _))
                    {
                        net.SkippedDrives++;
                        continue;
                    }
                    // Drives aren't locked to networks (owner's decision, 2026-11-12). Clear any
                    // lock stamp left by v0.5.0 the next time the server saves.
                    var slot = new Slot { Bay = storage, Index = i, ClearStamp = DriveItem.HasStamp(drive) };
                    drives.Add(new NetworkDrive(contents, cap, slot));
                }
            }

            net.Storage = new NetworkStorage(drives, ItemCodec.StackSizeOf);
            network = net;
            return true;
        }

        /// <summary>SERVER ONLY. Writes changed drives back into their bay slots.</summary>
        public void SaveDirty()
        {
            foreach (NetworkDrive d in Storage.Drives)
            {
                var slot = (Slot)d.Tag;
                if (!d.Dirty && !slot.ClearStamp) continue;
                ItemStack current = slot.Bay.ItemGrid[slot.Index];
                ItemValue updated = current.itemValue.Clone();
                if (d.Dirty) DriveItem.Save(updated, d.Contents);
                if (slot.ClearStamp) DriveItem.ClearStamp(updated);
                slot.Bay.UpdateSlot(slot.Index, new ItemStack(updated, current.count));
                slot.Bay.SetModified();
                slot.ClearStamp = false;
            }
        }

        /// <summary>Nearest Core (cube distance) whose own radius reaches <paramref name="from"/>.</summary>
        public static bool TryFindCore(World world, Vector3i from, out Vector3i corePos, out int coreRadius)
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
