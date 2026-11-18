using System;

namespace StashNet.Core.Protocol
{
    /// <summary>
    /// Loads a player's pins once and saves them after every toggle, through the given read/write functions (the game
    /// glue passes the real file). Read returning null = no file yet. If the read fails (file locked by antivirus or
    /// cloud sync...), pins still work for this session but the file is never written, so the saved pins can't be
    /// replaced by just the new ones. <see cref="Problem"/> holds the first error, for one log line.
    /// </summary>
    public sealed class FavoritesFile
    {
        private readonly Func<string> read;
        private readonly Action<string> write;
        private Favorites favorites;
        private bool readFailed;

        public FavoritesFile(Func<string> read, Action<string> write)
        {
            this.read = read;
            this.write = write;
        }

        /// <summary>The first read or save error, or null.</summary>
        public Exception Problem { get; private set; }

        public Favorites Current
        {
            get
            {
                if (favorites != null) return favorites;
                try
                {
                    favorites = Favorites.Parse(read());
                }
                catch (Exception e)
                {
                    readFailed = true;
                    Problem = Problem ?? e;
                    favorites = new Favorites();
                }
                return favorites;
            }
        }

        /// <summary>Pins or unpins the item and saves (unless the file couldn't be read). Returns whether it is pinned now.</summary>
        public bool Toggle(string itemName)
        {
            bool pinned = Current.Toggle(itemName);
            if (readFailed) return pinned;
            try
            {
                write(Current.Serialize());
            }
            catch (Exception e)
            {
                Problem = Problem ?? e;
            }
            return pinned;
        }
    }
}
