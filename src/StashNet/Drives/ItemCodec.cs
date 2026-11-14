using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace StashNet.Drives
{
    /// <summary>
    /// Converts game items to drive entries and back.
    ///
    /// Items are identified by NAME, never by numeric ID: numeric IDs shift when mods are added or
    /// removed, and the game's own binary item format depends on them (its reader even looks up the
    /// item class mid-read). So non-plain items get our own name-based encoding ("variant"):
    /// base64 of a small binary record holding name, durability, quality, metadata, stats, and
    /// nested mods/cosmetics (recursively, also by name).
    /// </summary>
    public static class ItemCodec
    {
        private const byte VariantVersion = 1;

        /// <summary>Plain = stacks with others of the same name and carries no per-item state.</summary>
        public static bool IsPlain(ItemValue iv)
        {
            ItemClass ic = iv?.ItemClass;
            if (ic == null || !ic.CanStack()) return false;
            if (iv.Quality != 0 || iv.UseTimes != 0f || iv.Meta != 0) return false;
            if (iv.Flags != 0 || iv.SelectedAmmoTypeIndex != 0) return false;
            if (iv.Metadata != null && iv.Metadata.Count > 0) return false;
            if (iv.Stats != null && iv.Stats.Length > 0) return false;
            if (HasAnyMod(iv.modifications) || HasAnyMod(iv.cosmeticMods)) return false;
            return true;
        }

        /// <summary>
        /// False for items a drive can't faithfully store yet (e.g. painted blocks). Those stay
        /// where they are rather than losing data.
        /// </summary>
        public static bool CanStore(ItemValue iv)
        {
            return iv != null && !iv.IsEmpty() && iv.ItemClass != null && iv.TextureFullArray.IsDefault;
        }

        public static string NameOf(ItemValue iv) => iv.ItemClass.GetItemName();

        /// <summary>Stack size by item name, for drive capacity. Unknown items count as 1.</summary>
        public static int StackSizeOf(string name)
        {
            ItemClass ic = ItemClass.GetItemClass(name);
            return ic == null ? 1 : System.Math.Max(1, ic.Stacknumber.Value);
        }

        /// <summary>Null for plain items; otherwise the name-based variant string.</summary>
        public static string EncodeVariant(ItemValue iv)
        {
            if (IsPlain(iv)) return null;
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms, Encoding.UTF8))
            {
                w.Write(VariantVersion);
                WriteItem(w, iv);
                w.Flush();
                return Convert.ToBase64String(ms.ToArray());
            }
        }

        private static readonly Dictionary<string, string> groupKeys = new Dictionary<string, string>();

        /// <summary>
        /// Terminal cell key: items with the same name and key show as one cell. No-quality items
        /// (engines, dyes, mods, animals) ignore the game's hidden random seed, which only rolls stat
        /// ranges on quality gear (owner, 2026-11-14: "No-quality items only"). Everything else, and
        /// anything that can't be decoded, keys on its exact variant. Storage is not affected.
        /// </summary>
        public static string GroupKey(string name, string variant)
        {
            if (variant == null) return null;
            if (groupKeys.TryGetValue(variant, out string cached)) return cached;
            string key = variant;
            if (TryDecode(name, variant, out ItemValue iv, out _) && iv.Quality == 0 && iv.Seed != 0)
            {
                iv.seed = 0;
                key = EncodeVariant(iv) ?? variant;
            }
            if (groupKeys.Count > 4096) groupKeys.Clear();
            groupKeys[variant] = key;
            return key;
        }

        /// <summary>Rebuilds a single item. False if the item (or one of its mods) no longer exists in this game.</summary>
        public static bool TryDecode(string name, string variant, out ItemValue iv, out string missingName)
        {
            iv = null;
            missingName = null;
            if (variant == null)
            {
                if (!TryNew(name, out iv)) missingName = name;
                return iv != null;
            }

            try
            {
                using (var ms = new MemoryStream(Convert.FromBase64String(variant)))
                using (var r = new BinaryReader(ms, Encoding.UTF8))
                {
                    if (r.ReadByte() != VariantVersion) return false;
                    iv = ReadItem(r, ref missingName);
                    return iv != null;
                }
            }
            catch (Exception e) when (e is FormatException || e is EndOfStreamException || e is IOException)
            {
                iv = null;
                return false;
            }
        }

        private static void WriteItem(BinaryWriter w, ItemValue iv)
        {
            w.Write(NameOf(iv));
            w.Write(iv.UseTimes);
            w.Write(iv.Quality);
            w.Write(iv.Meta);
            w.Write(iv.Flags);
            w.Write(iv.SelectedAmmoTypeIndex);
            w.Write(iv.Seed);

            var meta = new List<KeyValuePair<string, TypedMetadataValue>>();
            if (iv.Metadata != null)
            {
                foreach (var kv in iv.Metadata)
                {
                    if (kv.Value?.GetValue() != null) meta.Add(kv);
                }
            }
            w.Write(meta.Count);
            foreach (var kv in meta)
            {
                w.Write(kv.Key);
                TypedMetadataValue.TypeTag tag = kv.Value.GetTypeTag();
                w.Write((int)tag);
                switch (tag)
                {
                    case TypedMetadataValue.TypeTag.Integer: w.Write((int)kv.Value.GetValue()); break;
                    case TypedMetadataValue.TypeTag.Float: w.Write((float)kv.Value.GetValue()); break;
                    default: w.Write(kv.Value.GetValue().ToString()); break;
                }
            }

            int statCount = iv.Stats?.Length ?? 0;
            w.Write(statCount);
            for (int i = 0; i < statCount; i++)
            {
                ItemValue.Stat s = iv.Stats[i];
                w.Write((int)s.type);
                w.Write(s.isBoosted ? (short)0 : s.value);
                w.Write(s.isBoosted ? s.value : (short)0);
            }

            WriteMods(w, iv.modifications);
            WriteMods(w, iv.cosmeticMods);
        }

        private static void WriteMods(BinaryWriter w, ItemValue[] mods)
        {
            int n = mods?.Length ?? 0;
            w.Write(n);
            for (int i = 0; i < n; i++)
            {
                bool present = mods[i] != null && !mods[i].IsEmpty();
                w.Write(present);
                if (present) WriteItem(w, mods[i]);
            }
        }

        private static ItemValue ReadItem(BinaryReader r, ref string missingName)
        {
            string name = r.ReadString();
            float useTimes = r.ReadSingle();
            ushort quality = r.ReadUInt16();
            int meta = r.ReadInt32();
            byte flags = r.ReadByte();
            byte ammo = r.ReadByte();
            ushort seed = r.ReadUInt16();

            int metaCount = r.ReadInt32();
            var metadata = new List<KeyValuePair<string, TypedMetadataValue>>(metaCount);
            for (int i = 0; i < metaCount; i++)
            {
                string key = r.ReadString();
                var tag = (TypedMetadataValue.TypeTag)r.ReadInt32();
                object value;
                switch (tag)
                {
                    case TypedMetadataValue.TypeTag.Integer: value = r.ReadInt32(); break;
                    case TypedMetadataValue.TypeTag.Float: value = r.ReadSingle(); break;
                    default: value = r.ReadString(); break;
                }
                metadata.Add(new KeyValuePair<string, TypedMetadataValue>(key, new TypedMetadataValue(value, tag)));
            }

            int statCount = r.ReadInt32();
            ItemValue.Stat[] stats = statCount > 0 ? new ItemValue.Stat[statCount] : null;
            for (int i = 0; i < statCount; i++)
            {
                var type = (PassiveEffects)r.ReadInt32();
                short baseValue = r.ReadInt16();
                short added = r.ReadInt16();
                stats[i] = new ItemValue.Stat(type, baseValue, added);
            }

            ItemValue[] mods = ReadMods(r, ref missingName);
            ItemValue[] cosmetics = ReadMods(r, ref missingName);

            if (!TryNew(name, out ItemValue iv))
            {
                missingName = missingName ?? name;
                return null;
            }
            if (missingName != null) return null;

            iv.useTimes = useTimes;
            iv.quality = quality;
            iv.meta = meta;
            iv.flags = flags;
            iv.selectedAmmoTypeIndex = ammo;
            iv.seed = seed;
            foreach (var kv in metadata) iv.SetMetadata(kv.Key, kv.Value);
            iv.Stats = stats;
            iv.modifications = mods;
            iv.cosmeticMods = cosmetics;
            return iv;
        }

        private static ItemValue[] ReadMods(BinaryReader r, ref string missingName)
        {
            int n = r.ReadInt32();
            if (n == 0) return null;
            var mods = new ItemValue[n];
            for (int i = 0; i < n; i++)
            {
                mods[i] = r.ReadBoolean() ? ReadItem(r, ref missingName) : ItemValue.None.Clone();
            }
            return mods;
        }

        private static bool TryNew(string name, out ItemValue iv)
        {
            iv = null;
            ItemValue template = ItemClass.GetItem(name);
            if (template == null || template.IsEmpty() || template.ItemClass == null) return false;
            iv = new ItemValue(template.type);
            return true;
        }

        private static bool HasAnyMod(ItemValue[] mods)
        {
            if (mods == null) return false;
            foreach (ItemValue m in mods)
            {
                if (m != null && !m.IsEmpty()) return true;
            }
            return false;
        }
    }
}
