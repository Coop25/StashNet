using HarmonyLib;

namespace StashNet
{
    /// <summary>Entry point. The game finds this class and calls InitMod once at startup.</summary>
    public class StashNetMod : IModApi
    {
        public const string Version = "0.0.1";

        public void InitMod(Mod modInstance)
        {
            new Harmony("com.coop25.stashnet").PatchAll(typeof(StashNetMod).Assembly);
            Log.Out($"[StashNet] Loaded v{Version}");
        }
    }
}
