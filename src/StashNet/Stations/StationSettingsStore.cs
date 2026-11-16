using System;
using System.IO;
using StashNet.Core.Stations;

namespace StashNet.Stations
{
    /// <summary>
    /// SERVER ONLY. Every Network station's automation settings (fuel mode, stock rules, melt targets,
    /// output switch), saved as StashNet/stations.txt in the world's save folder, keyed by position and
    /// block name. Reloaded when the save folder changes (another world loaded).
    ///
    /// Never loses the file: if it can't be read (locked by antivirus, a disk hiccup), nothing is saved
    /// over it and the read is retried; an unreadable (corrupt) file is kept as a .bad copy before starting
    /// fresh; writes go to a temporary file that then replaces the real one.
    /// </summary>
    public static class StationSettingsStore
    {
        private const float RetrySeconds = 10f;

        private static string loadedDir;
        private static StationSettingsFile file;
        /// <summary>Set while the file exists but couldn't be read: settings are not saved until it can be.</summary>
        private static bool readFailed;
        private static float nextRetry;

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
                bool retry = readFailed && UnityEngine.Time.realtimeSinceStartup >= nextRetry;
                if (file == null || dir != loadedDir || retry)
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
            readFailed = false;
            if (string.IsNullOrEmpty(dir)) return new StationSettingsFile();
            string path = FilePath(dir);
            string text;
            try
            {
                if (!File.Exists(path)) return new StationSettingsFile();
                text = File.ReadAllText(path);
            }
            catch (Exception e)
            {
                readFailed = true;
                nextRetry = UnityEngine.Time.realtimeSinceStartup + RetrySeconds;
                Log.Warning($"[StashNet] couldn't read station settings (will retry, nothing is saved over them meanwhile): {e.Message}");
                return new StationSettingsFile();
            }
            try
            {
                return StationSettingsFile.Parse(text);
            }
            catch (Exception e)
            {
                string bad = path + ".bad-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                try { File.Copy(path, bad, true); } catch (Exception) { }
                Log.Error($"[StashNet] station settings file is damaged ({e.Message}); kept a copy as {Path.GetFileName(bad)} and started fresh.");
                return new StationSettingsFile();
            }
        }

        private static void Save()
        {
            if (string.IsNullOrEmpty(loadedDir)) return;
            if (readFailed)
            {
                Log.Warning("[StashNet] station settings not saved: the existing file couldn't be read yet (retrying).");
                return;
            }
            try
            {
                string path = FilePath(loadedDir);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, file.Serialize());
                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
            }
            catch (Exception e)
            {
                Log.Warning($"[StashNet] couldn't save station settings: {e.Message}");
            }
        }
    }
}
