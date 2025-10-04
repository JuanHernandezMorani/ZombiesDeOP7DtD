using System;

namespace ZombiesDeOPUnified.Core
{
    public static class CompatibilityManager
    {
        public static bool IsWar3zukInstalled =>
            Type.GetType("War3zukAIO.ModMain, War3zukAIO") != null ||
            Type.GetType("War3zukAIO.War3zukMain, War3zukAIO") != null;

        public static void ApplyCompatibilityPatches()
        {
            if (IsWar3zukInstalled)
            {
                ModConfig.Current.Detection.DetectionRange *= 0.8f;
                ModLogger.Log("Aplicando ajustes de compatibilidad para War3zuk AIO");
            }
        }
    }
}
