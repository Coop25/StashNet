using System;
using System.IO;
using StashNet.Core.Stations;

namespace StashNet.Stations
{
    /// <summary>
    /// SERVER ONLY. Every Network station's automation settings (fuel mode, stock rules), saved as
    /// StashNet/stations.txt in the world's save folder, keyed by position and block name.
    /// Reloaded when the save folder changes (another world loaded).
    /// </summary>
    public static class StationSettingsStore
    {
        private static string loadedDir;
        private static StationSettingsFile file;

        /// <summary>A copy (default settings when none are stored for this block here).</summary>
        public static StationSettings Get(Vector3i pos, Block block) =>
            Current.Get(pos.x, pos.y, pos.z, block?.GetBlockName() ?? "");

        public static void Set(Vector3i pos, Block block, StationSettings s)
        {
            Current.Set(pos.x, pos.y, pos.z, block?.GetBlockName() ?? "", s);
            Save();
        }

        /// <summary>The station was removed: a new block placed here starts fresh.</summary>
        public static void Remove(Vector3i pos)
        {
            if (Current.Remove(pos.x, pos.y, pos.z)) Save();
        }

        private static StationSettingsFile Current
        {
            get
            {
                string dir = SaveDir();
                if (file == null || dir != loadedDir)
                {
                    loadedDir = dir;
                    file = Load(dir);
                }
                return file;
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

        private static string FilePath(string dir) => Path.Combine(Path.Combine(dir, "StashNet"), "stations.txt");

        private static StationSettingsFile Load(string dir)
        {
            if (string.IsNullOrEmpty(dir)) return new StationSettingsFile();
            try
            {
                string path = FilePath(dir);
                return File.Exists(path) ? StationSettingsFile.Parse(File.ReadAllText(path)) : new StationSettingsFile();
            }
            catch (Exception e)
            {
                Log.Warning($"[StashNet] couldn't read station settings: {e.Message}");
                return new StationSettingsFile();
            }
        }

        private static void Save()
        {
            if (string.IsNullOrEmpty(loadedDir)) return;
            try
            {
                string path = FilePath(loadedDir);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, file.Serialize());
            }
            catch (Exception e)
            {
                Log.Warning($"[StashNet] couldn't save station settings: {e.Message}");
            }
        }
    }
}
