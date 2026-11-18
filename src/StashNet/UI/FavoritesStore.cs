using System.IO;
using StashNet.Core.Protocol;

namespace StashNet.UI
{
    /// <summary>
    /// PLAYER'S GAME: this player's pinned Storage Terminal items (owner, 2026-11-18: per player), in
    /// &lt;game user data&gt;/StashNet/favorites.txt, shared by every world and server. The load/save rules (read once,
    /// save on every change, never overwrite a file that couldn't be read) are in the game-free FavoritesFile.
    /// </summary>
    internal static class FavoritesStore
    {
        private static FavoritesFile file;
        private static bool warned;

        private static string FilePath => Path.Combine(GameIO.GetUserGameDataDir(), "StashNet", "favorites.txt");

        private static FavoritesFile File => file ?? (file = new FavoritesFile(Read, Write));

        public static Favorites Current
        {
            get
            {
                Favorites f = File.Current;
                WarnOnce();
                return f;
            }
        }

        /// <summary>Pins or unpins the item and saves. Returns whether it is pinned now.</summary>
        public static bool Toggle(string itemName)
        {
            bool pinned = File.Toggle(itemName);
            WarnOnce();
            return pinned;
        }

        private static string Read()
        {
            string path = FilePath;
            return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : null;
        }

        private static void Write(string text)
        {
            string path = FilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            System.IO.File.WriteAllText(path, text);
        }

        private static void WarnOnce()
        {
            if (warned || file?.Problem == null) return;
            warned = true;
            Log.Warning($"[StashNet] Terminal favorites file problem ({FilePath}): {file.Problem.Message}");
        }
    }
}
