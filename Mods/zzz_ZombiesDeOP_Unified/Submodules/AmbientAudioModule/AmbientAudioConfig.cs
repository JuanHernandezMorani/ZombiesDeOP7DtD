using ZombiesDeOPUnified.Core;

namespace ZombiesDeOPUnified.Submodules.AmbientAudioModule
{
    public static class AmbientAudioConfig
    {
        public static bool EnableAmbientAudio => ModConfig.Current.Modules.AmbientAudio && ModConfig.Current.AmbientAudio.Enabled;
        public static ModConfig.AmbientAudioSettings Settings => ModConfig.Current.AmbientAudio;
    }
}
