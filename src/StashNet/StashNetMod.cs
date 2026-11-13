using System.Linq;
using HarmonyLib;

namespace StashNet
{
    /// <summary>Entry point. The game finds this class and calls InitMod once at startup.</summary>
    public class StashNetMod : IModApi
    {
        public const string Version = "0.9.1";

        /// <summary>This mod's folder (Mods/StashNet), for files read at runtime such as Skins/.</summary>
        public static string ModPath { get; private set; }

        public void InitMod(Mod modInstance)
        {
            ModPath = modInstance.Path;
            var harmony = new Harmony("com.coop25.stashnet");
            harmony.PatchAll(typeof(StashNetMod).Assembly);
            Log.Out($"[StashNet] Loaded v{Version} ({harmony.GetPatchedMethods().Count()} methods patched)");
        }
    }
}
