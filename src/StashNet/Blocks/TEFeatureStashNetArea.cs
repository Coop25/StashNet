using System;
using Platform;
using StashNet.Network;
using UnityEngine;

namespace StashNet.Blocks
{
    /// <summary>
    /// Core feature (owner, 2026-11-13): "Show Area" / "Hide Area" in the Core's hold-E menu draws the same
    /// see-through box the land claim block uses, sized to this Core's reach (StashNetRadius, cube distance:
    /// 2 x reach + 1 blocks on every side, up and down too). Only the player who turned it on sees it, and it
    /// isn't saved: it goes away when the chunk unloads, the Core is removed, or the game restarts.
    /// Reuses vanilla's LandClaimBoundsHelper (keyed by position; a Core never shares a spot with a land claim)
    /// so the box follows origin shifts like a land claim's does. The game finds this class by name.
    /// </summary>
    public class TEFeatureStashNetArea : TEFeatureAbs
    {
        private const string CmdShow = "show_area";
        private const string CmdHide = "hide_area";

        private bool showing;

        public override void CopyFromInternal(TileEntityComposite _other)
        {
        }

        public override void InitBlockActivationCommands(Action<BlockActivationCommand, TileEntityComposite.EBlockCommandOrder, TileEntityFeatureData> _addCallback)
        {
            base.InitBlockActivationCommands(_addCallback);
            _addCallback(new BlockActivationCommand(CmdShow, "frames", _enabled: false), TileEntityComposite.EBlockCommandOrder.Normal, FeatureData);
            _addCallback(new BlockActivationCommand(CmdHide, "frames", _enabled: false), TileEntityComposite.EBlockCommandOrder.Normal, FeatureData);
        }

        public override bool AllowBlockActivationCommand(ITileEntityFeature _module, ReadOnlySpan<char> _commandName, WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityAlive _entityFocusing)
        {
            if (!base.AllowBlockActivationCommand(_module, _commandName, _world, _blockPos, _blockValue, _entityFocusing)) return false;
            if (!Equals(_module)) return true;
            if (LockedForMe()) return false;
            if (CommandIs(_commandName, CmdShow)) return !showing;
            if (CommandIs(_commandName, CmdHide)) return showing;
            return true;
        }

        public override bool OnBlockActivated(ReadOnlySpan<char> _commandName, WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
        {
            if (CommandIs(_commandName, CmdShow)) { Show(_blockValue); return true; }
            if (CommandIs(_commandName, CmdHide)) { Hide(); return true; }
            return base.OnBlockActivated(_commandName, _world, _blockPos, _blockValue, _player);
        }

        private void Show(BlockValue blockValue)
        {
            Transform box = LandClaimBoundsHelper.GetBoundsHelper(ToWorldPos().ToVector3());
            if (box == null) return;
            int reach = Math.Max(0, blockValue.Block.Properties.GetInt(StashNetNetwork.PropRadius));
            float side = 2 * reach + 1;
            box.localScale = new Vector3(side, side, side);
            box.gameObject.SetActive(true);
            showing = true;
        }

        private void Hide()
        {
            showing = false;
            LandClaimBoundsHelper.RemoveBoundsHelper(ToWorldPos().ToVector3());
        }

        public override void OnUnload(World _world)
        {
            base.OnUnload(_world);
            if (showing) Hide();
        }

        public override void OnRemove(World _world)
        {
            base.OnRemove(_world);
            if (showing) Hide();
        }

        /// <summary>Upgraded in place to another tier: the box would keep the old size, so drop it.</summary>
        public override void ReplacedBy(BlockValue _bvOld, BlockValue _bvNew, TileEntity _teNew)
        {
            base.ReplacedBy(_bvOld, _bvNew, _teNew);
            if (showing) Hide();
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            if (showing) Hide();
        }

        /// <summary>A locked Core shows its area only to players who know the code (like its other commands).</summary>
        private bool LockedForMe()
        {
            TEFeatureLockable lockable = Parent.GetFeature<TEFeatureLockable>();
            return lockable != null && lockable.IsLocked() && !lockable.IsUserAllowed(PlatformManager.InternalLocalUserIdentifier);
        }
    }
}
