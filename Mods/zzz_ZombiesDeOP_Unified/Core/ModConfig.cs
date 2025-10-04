using System;
using System.IO;
using System.Text;
using System.Timers;
using UnityEngine;

namespace ZombiesDeOPUnified.Core
{
    public static class ModConfig
    {
        private const string ConfigFileName = "config.json";
        private static readonly object SyncRoot = new();
        private static FileSystemWatcher watcher;
        private static Timer reloadTimer;
        private static ConfigData cachedConfig = ConfigData.CreateDefault();
        private static bool loaded;

        public static event Action ConfigReloaded;

        public static ConfigData Current
        {
            get
            {
                if (!loaded)
                {
                    Load();
                }

                return cachedConfig;
            }
        }

        public static void Load()
        {
            lock (SyncRoot)
            {
                if (loaded)
                {
                    return;
                }

                string path = GetConfigPath();
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);

                try
                {
                    if (!File.Exists(path))
                    {
                        cachedConfig = ConfigData.CreateDefault();
                        SaveInternal(path, cachedConfig);
                    }
                    else
                    {
                        string json = File.ReadAllText(path, Encoding.UTF8);
                        if (string.IsNullOrWhiteSpace(json))
                        {
                            cachedConfig = ConfigData.CreateDefault();
                            SaveInternal(path, cachedConfig);
                        }
                        else
                        {
                            cachedConfig = JsonUtility.FromJson<ConfigData>(json) ?? ConfigData.CreateDefault();
                        }
                    }
                }
                catch (Exception exception)
                {
                    ModLogger.Error($"No se pudo cargar {ConfigFileName}, se usarán valores por defecto: {exception}");
                    cachedConfig = ConfigData.CreateDefault();
                }
                finally
                {
                    loaded = true;
                    ConfigureWatcher(path);
                    ConfigReloaded?.Invoke();
                }
            }
        }

        public static void Save()
        {
            lock (SyncRoot)
            {
                string path = GetConfigPath();
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
                SaveInternal(path, cachedConfig);
            }
        }

        private static void SaveInternal(string path, ConfigData data)
        {
            try
            {
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(path, json, Encoding.UTF8);
            }
            catch (Exception exception)
            {
                ModLogger.Error($"Error al guardar la configuración en {path}: {exception}");
            }
        }

        private static void ConfigureWatcher(string path)
        {
            try
            {
                watcher?.Dispose();
                reloadTimer?.Dispose();

                string directory = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                {
                    return;
                }

                watcher = new FileSystemWatcher(directory, Path.GetFileName(path))
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true
                };

                reloadTimer = new Timer(350) { AutoReset = false };
                reloadTimer.Elapsed += (_, _) => ReloadFromDisk();

                FileSystemEventHandler handler = (_, _) => reloadTimer?.Start();
                watcher.Changed += handler;
                watcher.Created += handler;
                watcher.Renamed += (_, _) => reloadTimer?.Start();
            }
            catch (Exception exception)
            {
                ModLogger.Warning($"No se pudo vigilar cambios en la configuración: {exception.Message}");
            }
        }

        private static void ReloadFromDisk()
        {
            lock (SyncRoot)
            {
                string path = GetConfigPath();
                try
                {
                    if (!File.Exists(path))
                    {
                        return;
                    }

                    string json = File.ReadAllText(path, Encoding.UTF8);
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        return;
                    }

                    var parsed = JsonUtility.FromJson<ConfigData>(json);
                    if (parsed == null)
                    {
                        return;
                    }

                    cachedConfig = parsed;
                    ConfigReloaded?.Invoke();
                    ModLogger.Log("Configuración recargada desde disco.");
                }
                catch (Exception exception)
                {
                    ModLogger.Warning($"No se pudo recargar la configuración: {exception.Message}");
                }
            }
        }

        private static string GetConfigPath()
        {
            string assemblyPath = Path.GetDirectoryName(typeof(ModConfig).Assembly.Location) ?? string.Empty;
            string basePath = string.IsNullOrEmpty(assemblyPath)
                ? Environment.CurrentDirectory
                : Path.GetFullPath(Path.Combine(assemblyPath, ".."));
            return Path.Combine(basePath, "Config", ConfigFileName);
        }

        [Serializable]
        public class ConfigData
        {
            public ModuleToggles Modules = new();
            public DetectionSettings Detection = new();
            public DepthAwarenessSettings DepthAwareness = new();
            public InventoryButtonsSettings InventoryButtons = new();
            public StorageAccessSettings StorageAccess = new();
            public AmbientAudioSettings AmbientAudio = AmbientAudioSettings.CreateDefault();
            public FarmingSettings Farming = new();
            public VisualWeatherSettings VisualWeather = VisualWeatherSettings.CreateDefault();

            public static ConfigData CreateDefault() => new();
        }

        [Serializable]
        public class ModuleToggles
        {
            public bool DetectionSystem = true;
            public bool DetectionHUD = true;
            public bool DepthAwareness = true;
            public bool InventoryButtons = true;
            public bool ExtendedStorageReach = true;
            public bool AmbientAudio = true;
            public bool LiteFarming = true;
            public bool VisualWeather = true;
        }

        [Serializable]
        public class DetectionSettings
        {
            public bool DebugMode = false;
            public float DetectionRange = 50f;
            public float DetectionAngle = 120f;
            public float HearingRange = 25f;
            public float CheckInterval = 0.5f;
            public float HudUpdateInterval = 0.2f;
            public float HudOffsetX = 40f;
            public float HudOffsetY = 60f;
            public int FontSize = 18;
            public bool UseOutline = true;
        }

        [Serializable]
        public class DepthAwarenessSettings
        {
            public float DepthZeroDetection = 22f;
            public float DepthCritical = 20f;
            public float DepthHigh = 15f;
            public float DepthMedium = 10f;
            public float DepthLow = 5f;
            public float HorizontalFullEffect = 15f;
            public float HorizontalReducedEffect = 30f;
            public float HorizontalMinimalEffect = 50f;
            public float MinimalMultiplier = 0.1f;
        }

        [Serializable]
        public class InventoryButtonsSettings
        {
            public bool EnableSortButton = true;
            public bool EnableLootAllButton = true;
        }

        [Serializable]
        public class StorageAccessSettings
        {
            public float ReachDistance = 20f;
            public float MinimumVanillaDistance = 4f;
            public float ProximityTolerance = 5.5f;
        }

        [Serializable]
        public class AmbientAudioSettings
        {
            public bool Enabled = true;
            public float MasterVolume = 0.45f;
            public float Priority = 0.35f;
            public float DefaultFadeIn = 4f;
            public float DefaultFadeOut = 6f;
            public float DefaultRadius = 0f;
            public AmbientDefinition[] Sounds = Array.Empty<AmbientDefinition>();

            public static AmbientAudioSettings CreateDefault()
            {
                return new AmbientAudioSettings
                {
                    Sounds = new[]
                    {
                        new AmbientDefinition { Biome = "forest", Condition = "default", Clip = "ambient_forest_day", Volume = 1.0f },
                        new AmbientDefinition { Biome = "forest", Condition = "night", Clip = "ambient_forest_night", Volume = 0.85f },
                        new AmbientDefinition { Biome = "desert", Condition = "default", Clip = "ambient_desert_day", Volume = 0.95f },
                        new AmbientDefinition { Biome = "snow", Condition = "default", Clip = "ambient_snow_wind", Volume = 0.9f },
                        new AmbientDefinition { Biome = "wasteland", Condition = "default", Clip = "ambient_wasteland_low", Volume = 0.75f },
                        new AmbientDefinition { Biome = "any", Condition = "rain", Clip = "ambient_rain_light", Volume = 0.65f, FadeIn = 2.5f, FadeOut = 4f },
                        new AmbientDefinition { Biome = "any", Condition = "storm", Clip = "ambient_storm_heavy", Volume = 0.55f, FadeIn = 1.5f, FadeOut = 3.5f },
                        new AmbientDefinition { Biome = "any", Condition = "wind", Clip = "ambient_wind_gusts", Volume = 0.6f, Radius = 40f },
                        new AmbientDefinition { Biome = "any", Condition = "cave", Clip = "ambient_cave_drones", Volume = 0.7f, FadeIn = 3.5f, FadeOut = 5.5f },
                        new AmbientDefinition { Biome = "burntforest", Condition = "default", Clip = string.Empty, Volume = 0f }
                    }
                };
            }
        }

        [Serializable]
        public class AmbientDefinition
        {
            public string Biome = "any";
            public string Condition = "default";
            public string Clip = string.Empty;
            public float Volume = 1f;
            public float FadeIn = -1f;
            public float FadeOut = -1f;
            public float Radius = -1f;
            public float Priority = 0f;
        }

        [Serializable]
        public class FarmingSettings
        {
            public bool Enabled = true;
        }

        [Serializable]
        public class VisualWeatherSettings
        {
            public bool Enabled = true;
            public float TransitionSpeed = 2.5f;
            public float DawnStart = 0.18f;
            public float DayStart = 0.24f;
            public float DuskStart = 0.70f;
            public float NightStart = 0.80f;
            public FogSettings Fog = FogSettings.CreateDefault();
            public CloudSettings Clouds = CloudSettings.CreateDefault();
            public RainSettings Rain = RainSettings.CreateDefault();
            public SunSettings Sun = SunSettings.CreateDefault();

            public static VisualWeatherSettings CreateDefault() => new();
        }

        [Serializable]
        public class FogSettings
        {
            public bool Enabled = true;
            public float DayIntensity = 0.0022f;
            public float NightIntensity = 0.0055f;
            public Color DayColor = new(0.78f, 0.81f, 0.86f);
            public Color NightColor = new(0.22f, 0.26f, 0.33f);

            public static FogSettings CreateDefault() => new();
        }

        [Serializable]
        public class CloudSettings
        {
            public bool Enabled = true;
            public float DayDensity = 0.35f;
            public float NightDensity = 0.62f;

            public static CloudSettings CreateDefault() => new();
        }

        [Serializable]
        public class RainSettings
        {
            public bool Enabled = true;
            public float Opacity = 0.45f;
            public float DayIntensity = 0.15f;
            public float NightIntensity = 0.35f;

            public static RainSettings CreateDefault() => new();
        }

        [Serializable]
        public class SunSettings
        {
            public bool Enabled = true;
            public Color DawnColor = new(0.94f, 0.58f, 0.36f);
            public Color DayColor = new(0.83f, 0.87f, 0.92f);
            public Color DuskColor = new(0.68f, 0.45f, 0.78f);
            public Color NightColor = new(0.18f, 0.22f, 0.32f);

            public static SunSettings CreateDefault() => new();
        }
    }
}
