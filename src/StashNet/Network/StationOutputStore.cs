using System;
using System.IO;
using StashNet.Core.Network;

namespace StashNet.Network
{
    /// <summary>
    /// SERVER ONLY. Remembers which StashNet stations keep finished items instead of sending them
    /// into storage (on by default). Saved as StashNet/station-output.txt in the world's save folder,
    /// reloaded when the save folder changes (another world loaded).
    /// </summary>
    public static class StationOutputStore
    {
        private static string loadedDir;
        private static StationOutputSettings settings;

        public static bool IsOn(Vector3i pos) => Current.IsOn(pos.x, pos.y, pos.z);

        public static void Set(Vector3i pos, bool on)
        {
            if (Current.Set(pos.x, pos.y, pos.z, on)) Save();
        }

        private static StationOutputSettings Current
        {
            get
            {
                string dir = SaveDir();
                if (settings == null || dir != loadedDir)
                {
                    loadedDir = dir;
                    settings = Load(dir);
                }
                return settings;
            }
        }

        private static string SaveDir()
        {
            try
            {
                return GameIO.GetSaveGameDir();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string FilePath(string dir) => Path.Combine(Path.Combine(dir, "StashNet"), "station-output.txt");

        private static StationOutputSettings Load(string dir)
        {
            if (string.IsNullOrEmpty(dir)) return new StationOutputSettings();
            try
            {
                string path = FilePath(dir);
                return File.Exists(path) ? StationOutputSettings.Parse(File.ReadAllText(path)) : new StationOutputSettings();
            }
            catch (Exception e)
            {
                Log.Warning($"[StashNet] couldn't read station output settings: {e.Message}");
                return new StationOutputSettings();
            }
        }

        private static void Save()
        {
            if (string.IsNullOrEmpty(loadedDir)) return;
            try
            {
                string path = FilePath(loadedDir);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, settings.Serialize());
            }
            catch (Exception e)
            {
                Log.Warning($"[StashNet] couldn't save station output settings: {e.Message}");
            }
        }
    }
}
