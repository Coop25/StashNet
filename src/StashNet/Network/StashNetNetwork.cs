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
        /// <summary>Bays within this Core's reach that belong to a nearer Core (M8), so not in this network.</summary>
        public int OtherCoreBays { get; private set; }
        /// <summary>Bays skipped because someone has them open.</summary>
        public int SkippedBays { get; private set; }
        /// <summary>Drives skipped because their data can't be read (left untouched).</summary>
        public int SkippedDrives { get; private set; }
        /// <summary>Sum of every drive's heat weight (readable or not) in bays nobody has open (M8).</summary>
        public float HeatWeight { get; private set; }

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
            // Every Core that could claim a bay within this Core's reach, gathered once (M8).
            List<CoreSite> cores = CoresNear(world, corePos, radius + MaxCoreRadius());

            var drives = new List<NetworkDrive>();
            foreach (TileEntityComposite te in TileEntityScan.InCube(world, corePos, radius))
            {
                if (te.GetFeature<TEFeatureStashNetDriveBay>() == null) continue;
                TEFeatureStorage storage = te.GetFeature<TEFeatureStorage>();
                if (storage == null) continue;
                // A bay belongs to its nearest Core only, so it is never in two networks (M8).
                if (!IsOwner(cores, te.ToWorldPos(), corePos))
                {
                    net.OtherCoreBays++;
                    continue;
                }

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
                    net.HeatWeight += DriveItem.HeatWeight(drive); // readable or not (M8)

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

        /// <summary>
        /// The Core that owns <paramref name="from"/>: the nearest Core (cube distance) whose own radius
        /// reaches it, ties broken by position (StashNet.Core CorePicker). Terminals, bays and lock
        /// checks all use this one rule.
        /// </summary>
        public static bool TryFindCore(World world, Vector3i from, out Vector3i corePos, out int coreRadius)
        {
            corePos = default;
            coreRadius = 0;
            List<CoreSite> cores = CoresNear(world, from, MaxCoreRadius());
            int i = CorePicker.Pick(cores, from.x, from.y, from.z);
            if (i < 0) return false;
            corePos = new Vector3i(cores[i].X, cores[i].Y, cores[i].Z);
            coreRadius = cores[i].Reach;
            return true;
        }

        /// <summary>Every Core tile entity within cube distance <paramref name="r"/>, with its reach.</summary>
        private static List<CoreSite> CoresNear(World world, Vector3i center, int r)
        {
            var cores = new List<CoreSite>();
            foreach (TileEntityComposite te in TileEntityScan.InCube(world, center, r))
            {
                Vector3i pos = te.ToWorldPos();
                Block block = world.GetBlock(pos).Block;
                if (!IsCore(block)) continue;
                cores.Add(new CoreSite(pos.x, pos.y, pos.z, block.Properties.GetInt(PropRadius)));
            }
            return cores;
        }

        private static bool IsOwner(List<CoreSite> cores, Vector3i pos, Vector3i corePos)
        {
            int i = CorePicker.Pick(cores, pos.x, pos.y, pos.z);
            return i >= 0 && cores[i].X == corePos.x && cores[i].Y == corePos.y && cores[i].Z == corePos.z;
        }

        /// <summary>
        /// Owner's spacing rule: true if a Core placed at <paramref name="pos"/> would be within
        /// 2 x the largest Core reach of an existing Core (StashNet.Core CoreSpacing). Loaded chunks only.
        /// </summary>
        public static bool CoreTooClose(World world, Vector3i pos, out int distance, out int minGap)
        {
            int maxReach = MaxCoreRadius();
            minGap = CoreSpacing.MinGap(maxReach);
            List<CoreSite> cores = CoresNear(world, pos, minGap);
            return CoreSpacing.TooClose(cores, pos.x, pos.y, pos.z, maxReach, out distance);
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
    }
}
