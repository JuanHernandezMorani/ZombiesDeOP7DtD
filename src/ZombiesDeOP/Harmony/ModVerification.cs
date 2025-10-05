using HarmonyLib;
using ZombiesDeOP.Compatibility;
using ZombiesDeOP.Utilities;

namespace ZombiesDeOP.Harmony
{
    [HarmonyPatch(typeof(GameManager))]
    [HarmonyPatch("ShowLoadingScreen")]
    public static class ModVerification
    {
        private static void Postfix()
        {
            ModLogger.Info("🎯 [ZombiesDeOP] Mod activo - Versión 2.4 Compatible");
            ModLogger.Info($"🔧 [ZombiesDeOP] Detección: {ModSettings.DetectionRange}m");
            ModLogger.Info($"🔧 [ZombiesDeOP] War3zuk: {War3zukCompatibility.IsWar3zukInstalled}");
        }
    }
}
