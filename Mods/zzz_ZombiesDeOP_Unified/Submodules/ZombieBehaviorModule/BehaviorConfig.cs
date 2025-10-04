using ZombiesDeOPUnified.Core;

namespace ZombiesDeOPUnified.Submodules.ZombieBehaviorModule
{
    public static class BehaviorConfig
    {
        public static bool EnableDepthAwareness => ModConfig.Current.Modules.DepthAwareness;
        public static float DepthZeroDetection => ModConfig.Current.DepthAwareness.DepthZeroDetection;
        public static float DepthCritical => ModConfig.Current.DepthAwareness.DepthCritical;
        public static float DepthHigh => ModConfig.Current.DepthAwareness.DepthHigh;
        public static float DepthMedium => ModConfig.Current.DepthAwareness.DepthMedium;
        public static float DepthLow => ModConfig.Current.DepthAwareness.DepthLow;
        public static float HorizontalFullEffect => ModConfig.Current.DepthAwareness.HorizontalFullEffect;
        public static float HorizontalReducedEffect => ModConfig.Current.DepthAwareness.HorizontalReducedEffect;
        public static float HorizontalMinimalEffect => ModConfig.Current.DepthAwareness.HorizontalMinimalEffect;
        public static float MinimalMultiplier => ModConfig.Current.DepthAwareness.MinimalMultiplier;
    }
}
