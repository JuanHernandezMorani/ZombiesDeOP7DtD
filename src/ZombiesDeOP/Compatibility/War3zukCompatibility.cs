using ZombiesDeOP.Utilities;

namespace ZombiesDeOP.Compatibility
{
    public static class War3zukCompatibility
    {
        public static bool IsWar3zukInstalled
        {
            get
            {
                return ModManager.GetLoadedMods()?.Exists(mod => mod != null && mod.Name.Equals("War3zukAIO")) ?? false;
            }
        }

        public static void ApplyCompatibility()
        {
            if (IsWar3zukInstalled)
            {
                ModSettings.DetectionRange *= 0.85f;
                ModSettings.HearingRange *= 0.9f;
                ModLogger.Log("🔧 [ZombiesDeOP] Aplicando compatibilidad War3zuk AIO");
            }
            else
            {
                ModLogger.Debug("War3zuk AIO no detectado, usando configuración estándar");
            }
        }
    }
}
