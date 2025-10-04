using ZombiesDeOPUnified.Core;

namespace ZombiesDeOPUnified.Submodules.VisualWeatherModule
{
    public static class VisualWeatherConfig
    {
        public static bool EnableVisualWeather => ModConfig.Current.Modules.VisualWeather && ModConfig.Current.VisualWeather.Enabled;
        public static ModConfig.VisualWeatherSettings Settings => ModConfig.Current.VisualWeather;
    }
}
