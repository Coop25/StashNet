using System;
using System.IO;
using StashNet.Core.Protocol;

namespace StashNet.UI
{
    /// <summary>
    /// PLAYER'S GAME: this player's pinned Storage Terminal items (owner, 2026-11-18: per player), in
    /// &lt;game user data&gt;/StashNet/favorites.txt, shared by every world and server. Read once on first use and written
    /// on every change. A missing or unreadable file means no pins; problems are logged once and never break the terminal.
    /// </summary>
    internal static class FavoritesStore
    {
        private static Favorites favorites;
        private static bool warned;

        private static string FilePath => Path.Combine(GameIO.GetUserGameDataDir(), "StashNet", "favorites.txt");

        public static Favorites Current
        {
            get
            {
                if (favorites != null) return favorites;
                try
                {
                    string path = FilePath;
                    favorites = File.Exists(path) ? Favorites.Parse(File.ReadAllText(path)) : new Favorites();
                }
                catch (Exception e)
                {
                    Warn("read", e);
                    favorites = new Favorites();
                }
                return favorites;
            }
        }

        /// <summary>Pins or unpins the item and saves. Returns whether it is pinned now.</summary>
        public static bool Toggle(string itemName)
        {
            bool pinned = Current.Toggle(itemName);
            try
            {
                string path = FilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, Current.Serialize());
            }
            catch (Exception e)
            {
                Warn("save", e); // the pin still works until the game closes
            }
            return pinned;
        }

        private static void Warn(string what, Exception e)
        {
            if (warned) return;
            warned = true;
            Log.Warning($"[StashNet] Couldn't {what} the terminal favorites file: {e.Message}");
        }
    }
}
