using ZombiesDeOPUnified.Core;

namespace ZombiesDeOPUnified.Submodules.InventoryEnhancements
{
    public static class InventoryEnhancementConfig
    {
        public static bool EnableInventoryButtons => ModConfig.Current.Modules.InventoryButtons;
        public static bool EnableSortButton => ModConfig.Current.InventoryButtons.EnableSortButton;
        public static bool EnableLootAllButton => ModConfig.Current.InventoryButtons.EnableLootAllButton;
    }
}
