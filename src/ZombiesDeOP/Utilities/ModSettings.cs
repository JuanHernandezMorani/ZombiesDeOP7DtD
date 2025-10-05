using System;
using System.IO;
using System.Xml;
using UnityEngine;

namespace ZombiesDeOP.Utilities
{
    public static class ModSettings
    {
        public static float DetectionRange { get; set; } = 45f;
        public static float HearingRange { get; set; } = 20f;
        public static bool EnableHUD { get; set; } = true;
        public static bool DebugMode { get; set; } = false;

        private const string CONFIG_FILE_NAME = "settings.xml";
        private static readonly string ConfigDirectory = Path.Combine(Application.persistentDataPath, "Mods", "ZombiesDeOP");

        public static void Load()
        {
            string configPath = GetConfigPath();

            string bundledConfigPath = ModContext.ResolveConfigPath(Path.Combine("Config", CONFIG_FILE_NAME));

            if (!File.Exists(configPath) && File.Exists(bundledConfigPath))
            {
                File.Copy(bundledConfigPath, configPath, overwrite: true);
            }

            if (!File.Exists(configPath))
            {
                CreateDefaultConfig();
                return;
            }

            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(configPath);

                if (doc.DocumentElement == null)
                {
                    CreateDefaultConfig();
                    return;
                }

                var root = doc.DocumentElement;
                DetectionRange = GetFloatValue(root, "DetectionRange", 45f);
                HearingRange = GetFloatValue(root, "HearingRange", 20f);
                EnableHUD = GetBoolValue(root, "EnableHUD", true);
                DebugMode = GetBoolValue(root, "DebugMode", false);

                ModLogger.Info("✅ [ZombiesDeOP] Configuración cargada");
            }
            catch (Exception e)
            {
                ModLogger.Error($"❌ [ZombiesDeOP] Error cargando configuración: {e}");
                CreateDefaultConfig();
            }
        }

        private static void CreateDefaultConfig()
        {
            try
            {
                string configPath = GetConfigPath();
                string directory = Path.GetDirectoryName(configPath) ?? ConfigDirectory;

                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using StreamWriter writer = new StreamWriter(configPath);
                writer.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
                writer.WriteLine("<ZombiesDeOP>");
                writer.WriteLine($"  <DetectionRange>{DetectionRange}</DetectionRange>");
                writer.WriteLine($"  <HearingRange>{HearingRange}</HearingRange>");
                writer.WriteLine($"  <EnableHUD>{EnableHUD.ToString().ToLowerInvariant()}</EnableHUD>");
                writer.WriteLine($"  <DebugMode>{DebugMode.ToString().ToLowerInvariant()}</DebugMode>");
                writer.WriteLine("</ZombiesDeOP>");

                ModLogger.Info("✅ [ZombiesDeOP] Configuración por defecto creada");
            }
            catch (Exception e)
            {
                ModLogger.Error($"❌ [ZombiesDeOP] Error creando configuración: {e}");
            }
        }

        private static string GetConfigPath()
        {
            return Path.Combine(ConfigDirectory, CONFIG_FILE_NAME);
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

                if (float.TryParse(node.InnerText, out float result))
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
    }
}
