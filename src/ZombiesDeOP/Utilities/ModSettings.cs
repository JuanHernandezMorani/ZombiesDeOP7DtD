using System;
using System.Globalization;
using System.IO;
using System.Xml;
using UnityEngine;

namespace ZombiesDeOP.Utilities
{
    public static class ModSettings
    {
        private const float DEFAULT_DETECTION_RANGE = 45f;
        private const float DEFAULT_HEARING_RANGE = 20f;
        private const float DEFAULT_POLLING_INTERVAL = 0.3f;

        public static float DetectionRange { get; internal set; } = DEFAULT_DETECTION_RANGE;
        public static float HearingRange { get; internal set; } = DEFAULT_HEARING_RANGE;
        public static float PollingInterval { get; private set; } = DEFAULT_POLLING_INTERVAL;
        public static bool EnableHUD { get; private set; } = true;
        public static bool EnableHarmonyPatch { get; private set; } = true;
        public static bool DebugMode { get; private set; } = false;

        private const string CONFIG_FILE_NAME = "settings.xml";

        public static void Load()
        {
            try
            {
                string configPath = PrepareConfiguration();
                EnsureConfigFileExists(configPath);
                LoadFromFile(configPath);
            }
            catch (Exception e)
            {
                ModLogger.Warn($"⚠️ [ZombiesDeOP] No se pudo cargar configuración, usando valores por defecto: {e.Message}");
                ResetToDefaults();
            }
        }

        private static string PrepareConfiguration()
        {
            string directory = EnsureConfigDirectory();
            return Path.Combine(directory, CONFIG_FILE_NAME);
        }

        private static void EnsureConfigFileExists(string configPath)
        {
            if (File.Exists(configPath))
            {
                return;
            }

            if (TryCopyBundledConfig(configPath))
            {
                return;
            }

            CreateDefaultConfig(configPath);
        }

        private static void LoadFromFile(string configPath)
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(configPath);

                if (doc.DocumentElement == null)
                {
                    CreateDefaultConfig(configPath);
                    return;
                }

                ResetToDefaults();

                var root = doc.DocumentElement;
                DetectionRange = Mathf.Clamp(GetFloatValue(root, nameof(DetectionRange), DEFAULT_DETECTION_RANGE), 5f, 80f);
                HearingRange = Mathf.Max(0f, GetFloatValue(root, nameof(HearingRange), DEFAULT_HEARING_RANGE));
                PollingInterval = Mathf.Clamp(GetFloatValue(root, nameof(PollingInterval), DEFAULT_POLLING_INTERVAL), 0.1f, 1f);
                EnableHUD = GetBoolValue(root, nameof(EnableHUD), true);
                EnableHarmonyPatch = GetBoolValue(root, nameof(EnableHarmonyPatch), true);
                DebugMode = GetBoolValue(root, nameof(DebugMode), false);

                ModLogger.Info($"✅ [ZombiesDeOP] Configuración cargada desde {configPath}");
            }
            catch (Exception e)
            {
                ModLogger.Error("❌ [ZombiesDeOP] Error leyendo configuración", e);
                CreateDefaultConfig(configPath);
            }
        }

        private static void CreateDefaultConfig(string configPath)
        {
            try
            {
                ResetToDefaults();

                string directory = Path.GetDirectoryName(configPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using StreamWriter writer = new StreamWriter(configPath);
                writer.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
                writer.WriteLine("<ZombiesDeOP>");
                writer.WriteLine($"  <{nameof(PollingInterval)}>{PollingInterval.ToString(CultureInfo.InvariantCulture)}</{nameof(PollingInterval)}>");
                writer.WriteLine($"  <{nameof(DetectionRange)}>{DetectionRange.ToString(CultureInfo.InvariantCulture)}</{nameof(DetectionRange)}>");
                writer.WriteLine($"  <{nameof(HearingRange)}>{HearingRange.ToString(CultureInfo.InvariantCulture)}</{nameof(HearingRange)}>");
                writer.WriteLine($"  <{nameof(EnableHUD)}>{EnableHUD.ToString().ToLowerInvariant()}</{nameof(EnableHUD)}>");
                writer.WriteLine($"  <{nameof(EnableHarmonyPatch)}>{EnableHarmonyPatch.ToString().ToLowerInvariant()}</{nameof(EnableHarmonyPatch)}>");
                writer.WriteLine($"  <{nameof(DebugMode)}>{DebugMode.ToString().ToLowerInvariant()}</{nameof(DebugMode)}>");
                writer.WriteLine("</ZombiesDeOP>");

                ModLogger.Info("✅ [ZombiesDeOP] Configuración por defecto creada");
            }
            catch (Exception e)
            {
                ModLogger.Error("❌ [ZombiesDeOP] Error creando configuración", e);
            }
        }

        private static float GetFloatValue(XmlElement root, string key, float defaultValue)
        {
            try
            {
                var node = root.SelectSingleNode(key);
                if (node == null || string.IsNullOrEmpty(node.InnerText))
                {
                    return defaultValue;
                }

                if (float.TryParse(node.InnerText, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
                {
                    return result;
                }
            }
            catch (Exception e)
            {
                ModLogger.LogDebug($"No se pudo leer el valor float '{key}': {e.Message}");
            }

            return defaultValue;
        }

        private static bool GetBoolValue(XmlElement root, string key, bool defaultValue)
        {
            try
            {
                var node = root.SelectSingleNode(key);
                if (node == null || string.IsNullOrEmpty(node.InnerText))
                {
                    return defaultValue;
                }

                if (bool.TryParse(node.InnerText, out bool result))
                {
                    return result;
                }
            }
            catch (Exception e)
            {
                ModLogger.LogDebug($"No se pudo leer el valor boolean '{key}': {e.Message}");
            }

            return defaultValue;
        }

        private static string EnsureConfigDirectory()
        {
            string baseDirectory = NormalizeBaseDirectory(ResolveSaveDirectory());

            if (string.IsNullOrEmpty(baseDirectory))
            {
                baseDirectory = Application.persistentDataPath;
            }

            string lastSegment = Path.GetFileName(baseDirectory);
            string modDirectory = (lastSegment != null && lastSegment.Equals("Mods", StringComparison.OrdinalIgnoreCase))
                ? Path.Combine(baseDirectory, "ZombiesDeOP")
                : Path.Combine(baseDirectory, "Mods", "ZombiesDeOP");

            if (!Directory.Exists(modDirectory))
            {
                Directory.CreateDirectory(modDirectory);
                ModLogger.Info($"🗂️ [ZombiesDeOP] Directorio de configuración preparado: {modDirectory}");
            }

            return modDirectory;
        }

        private static bool TryCopyBundledConfig(string destinationPath)
        {
            try
            {
                string bundledConfigPath = ModContext.ResolveConfigPath(Path.Combine("Config", CONFIG_FILE_NAME));
                if (!string.IsNullOrEmpty(bundledConfigPath) && File.Exists(bundledConfigPath))
                {
                    File.Copy(bundledConfigPath, destinationPath, overwrite: false);
                    ModLogger.Info("🛠️ [ZombiesDeOP] Configuración base copiada desde paquete del mod");
                    return true;
                }
            }
            catch (Exception e)
            {
                ModLogger.Warn($"⚠️ [ZombiesDeOP] No se pudo copiar configuración incluida: {e.Message}");
            }

            return false;
        }

        private static string ResolveSaveDirectory()
        {
            try
            {
                string saveDir = GameIO.GetSaveGameDir();
                if (!string.IsNullOrEmpty(saveDir))
                {
                    return saveDir;
                }
            }
            catch (Exception e)
            {
                ModLogger.LogDebug($"No se pudo resolver GameIO.GetSaveGameDir(): {e.Message}");
            }

            try
            {
                string persistent = Application.persistentDataPath;
                if (!string.IsNullOrEmpty(persistent))
                {
                    return persistent;
                }
            }
            catch (Exception e)
            {
                ModLogger.LogDebug($"Application.persistentDataPath no disponible: {e.Message}");
            }

            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return string.IsNullOrEmpty(roaming)
                ? Path.Combine(Path.GetTempPath(), "7DaysToDie")
                : Path.Combine(roaming, "7DaysToDie");
        }

        private static string NormalizeBaseDirectory(string baseDirectory)
        {
            if (string.IsNullOrEmpty(baseDirectory))
            {
                return baseDirectory;
            }

            string current = baseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string candidate = current;

            while (!string.IsNullOrEmpty(candidate))
            {
                string segment = Path.GetFileName(candidate);
                if (segment != null && segment.Equals("Saves", StringComparison.OrdinalIgnoreCase))
                {
                    string parent = Path.GetDirectoryName(candidate);
                    return string.IsNullOrEmpty(parent) ? candidate : parent;
                }

                string next = Path.GetDirectoryName(candidate);
                if (string.IsNullOrEmpty(next) || next == candidate)
                {
                    break;
                }

                candidate = next;
            }

            return current;
        }

        private static void ResetToDefaults()
        {
            DetectionRange = DEFAULT_DETECTION_RANGE;
            HearingRange = DEFAULT_HEARING_RANGE;
            PollingInterval = DEFAULT_POLLING_INTERVAL;
            EnableHUD = true;
            EnableHarmonyPatch = true;
            DebugMode = false;
        }
    }
}
