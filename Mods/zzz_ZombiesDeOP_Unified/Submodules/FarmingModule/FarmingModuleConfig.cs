using ZombiesDeOPUnified.Core;

namespace ZombiesDeOPUnified.Submodules.FarmingModule
{
    public static class FarmingModuleConfig
    {
        public static bool EnableLiteFarming => ModConfig.Current.Modules.LiteFarming && ModConfig.Current.Farming.Enabled;
    }
}
