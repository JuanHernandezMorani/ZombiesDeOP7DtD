using ZombiesDeOPUnified.Core;

namespace ZombiesDeOPUnified.Submodules.StorageAccessModule
{
    public static class StorageAccessConfig
    {
        public static bool EnableExtendedReach => ModConfig.Current.Modules.ExtendedStorageReach;
        public static float ReachDistance => ModConfig.Current.StorageAccess.ReachDistance;
        public static float MinimumVanillaDistance => ModConfig.Current.StorageAccess.MinimumVanillaDistance;
        public static float ProximityTolerance => ModConfig.Current.StorageAccess.ProximityTolerance;
    }
}
