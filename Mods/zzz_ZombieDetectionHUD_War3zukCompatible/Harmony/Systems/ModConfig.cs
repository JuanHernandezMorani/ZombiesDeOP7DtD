using System;
using System.IO;
using UnityEngine;

namespace ZombieDetectionHUD
{
    /// <summary>
    /// Loads and persists runtime configuration from a JSON settings file.
    /// </summary>
    public static class ModConfig
    {
        private const string ConfigFileName = "settings.json";
        private static readonly object SyncRoot = new();
        private static bool loaded;
        private static ConfigData data = ConfigData.CreateDefault();

        public static float DetectionRange => data.DetectionRange;
        public static float DetectionAngle => data.DetectionAngle;
        public static float HearingRange => data.HearingRange;
        public static float CheckInterval => data.CheckInterval;
        public static float HudUpdateInterval => data.HudUpdateInterval;
        public static bool ShowHud => data.ShowHud;
        public static bool DebugMode => data.DebugMode;
        public static Vector2 HudOffset => new(data.HudOffsetX, data.HudOffsetY);
        public static int FontSize => data.FontSize;
        public static bool UseOutline => data.UseOutline;

        /// <summary>
        /// Loads the configuration from disk, creating the file if necessary.
        /// </summary>
        public static void LoadConfig()
        {
            if (loaded)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (loaded)
                {
                    return;
                }

                try
                {
                    Directory.CreateDirectory(ModPaths.ConfigDirectory);
                    var path = Path.Combine(ModPaths.ConfigDirectory, ConfigFileName);
                    if (!File.Exists(path))
                    {
                        ModLogger.Info("No se encontró configuración. Generando archivo por defecto...");
                        data = ConfigData.CreateDefault();
                        SaveConfigInternal(path, data);
                        loaded = true;
                        return;
                    }

                    var json = File.ReadAllText(path);
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        ModLogger.Warning("El archivo de configuración estaba vacío. Se usarán valores por defecto.");
                        data = ConfigData.CreateDefault();
                        SaveConfigInternal(path, data);
                        loaded = true;
                        return;
                    }

                    var parsed = JsonUtility.FromJson<ConfigData>(json);
                    if (parsed == null)
                    {
                        ModLogger.Warning("No se pudo analizar el archivo de configuración. Se usarán valores por defecto.");
                        data = ConfigData.CreateDefault();
                        SaveConfigInternal(path, data);
                        loaded = true;
                        return;
                    }

                    data = parsed;
                    loaded = true;
                    ModLogger.Info("Configuración del HUD cargada correctamente.");
                }
                catch (Exception ex)
                {
                    ModLogger.Error("Error al cargar la configuración. Se usarán valores por defecto.", ex);
                    data = ConfigData.CreateDefault();
                }
            }
        }

        /// <summary>
        /// Writes the current configuration values back to disk.
        /// </summary>
        public static void SaveConfig()
        {
            lock (SyncRoot)
            {
                try
                {
                    Directory.CreateDirectory(ModPaths.ConfigDirectory);
                    var path = Path.Combine(ModPaths.ConfigDirectory, ConfigFileName);
                    SaveConfigInternal(path, data);
                    ModLogger.Info("Configuración guardada correctamente.");
                }
                catch (Exception ex)
                {
                    ModLogger.Error("No se pudo guardar la configuración del HUD.", ex);
                }
            }
        }

        private static void SaveConfigInternal(string path, ConfigData config)
        {
            try
            {
                var json = JsonUtility.ToJson(config, true);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                ModLogger.Error($"No se pudo escribir el archivo de configuración en {path}.", ex);
            }
        }

        [Serializable]
        private sealed class ConfigData
        {
            public float DetectionRange = 50f;
            public float DetectionAngle = 120f;
            public float HearingRange = 25f;
            public float CheckInterval = 0.5f;
            public float HudUpdateInterval = 0.2f;
            public bool ShowHud = true;
            public bool DebugMode = false;
            public float HudOffsetX = 40f;
            public float HudOffsetY = 60f;
            public int FontSize = 18;
            public bool UseOutline = true;

            public static ConfigData CreateDefault() => new();
        }
    }
}
