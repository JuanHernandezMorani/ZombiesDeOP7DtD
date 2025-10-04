using UnityEngine;
using ZombiesDeOPUnified.Core;

namespace ZombiesDeOPUnified.Submodules.ZombieDetectionSystem
{
    public static class DetectionConfig
    {
        public static bool EnableDetectionSystem => ModConfig.Current.Modules.DetectionSystem;
        public static bool EnableDetectionHUD => ModConfig.Current.Modules.DetectionHUD;
        public static bool DebugMode => ModConfig.Current.Detection.DebugMode;
        public static float BaseDetectionRange => ModConfig.Current.Detection.DetectionRange;
        public static float DetectionAngle => ModConfig.Current.Detection.DetectionAngle;
        public static float HearingRange => ModConfig.Current.Detection.HearingRange;
        public static float CheckInterval => Mathf.Max(0.05f, ModConfig.Current.Detection.CheckInterval);
        public static float HudUpdateInterval => Mathf.Max(0.05f, ModConfig.Current.Detection.HudUpdateInterval);
        public static Vector2 HudOffset => new(ModConfig.Current.Detection.HudOffsetX, ModConfig.Current.Detection.HudOffsetY);
        public static int FontSize => Mathf.Max(10, ModConfig.Current.Detection.FontSize);
        public static bool UseOutline => ModConfig.Current.Detection.UseOutline;
    }
}
