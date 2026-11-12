using System.Linq;
using HarmonyLib;

namespace StashNet
{
    /// <summary>Entry point. The game finds this class and calls InitMod once at startup.</summary>
    public class StashNetMod : IModApi
    {
        public const string Version = "0.7.2";

        public void InitMod(Mod modInstance)
        {
            var harmony = new Harmony("com.coop25.stashnet");
            harmony.PatchAll(typeof(StashNetMod).Assembly);
            Log.Out($"[StashNet] Loaded v{Version} ({harmony.GetPatchedMethods().Count()} methods patched)");
        }
    }
}
