using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using HarmonyLib;
using UnityEngine;

namespace ClimaVisual
{
    /// <summary>
    /// Harmony bootstrapper that initialises and ticks the climate visual controller.
    /// </summary>
    [HarmonyPatch]
    public static class ClimaVisualBootstrap
    {
        private static bool s_initialised;

        [HarmonyPatch(typeof(GameManager), "Awake")]
        [HarmonyPostfix]
        private static void OnGameAwake(GameManager __instance)
        {
            if (s_initialised)
            {
                return;
            }

            ClimaVisualManager.Initialize();
            s_initialised = true;
        }

        [HarmonyPatch(typeof(GameManager), "Update")]
        [HarmonyPostfix]
        private static void OnGameUpdate()
        {
            if (!s_initialised)
            {
                return;
            }

            ClimaVisualManager.Tick();
        }
    }

    internal static class ClimaVisualManager
    {
        private const float DefaultDayLength = 24000f;

        private static readonly int RainOpacityShaderId = Shader.PropertyToID("_ClimaVisual_RainOpacity");
        private static readonly int CloudDensityShaderId = Shader.PropertyToID("_ClimaVisual_CloudDensity");

        private static VisualSettings _settings = VisualSettings.CreateDefault();
        private static FileSystemWatcher _watcher;
        private static float _smoothedFogDensity;
        private static Color _smoothedFogColor = Color.black;
        private static Color _smoothedAmbientColor = Color.black;
        private static volatile bool _pendingReload;
        private static DateTime _reloadRequestedAt = DateTime.MinValue;

        internal static void Initialize()
        {
            LoadConfiguration();
            SetupWatcher();
        }

        internal static void Tick()
        {
            if (_settings == null || !_settings.Enabled)
            {
                return;
            }

            var gameManager = GameManager.Instance;
            var world = gameManager?.World;
            if (world == null)
            {
                return;
            }

            TryReloadIfRequested();

            float normalizedTime = GetNormalizedTime(world);
            float daylightFactor = EvaluateDaylightFactor(normalizedTime);
            float deltaTime = Time.deltaTime <= 0f ? 0.016f : Time.deltaTime;

            if (_settings.Fog.Enabled)
            {
                ApplyFogSettings(daylightFactor, deltaTime);
            }

            if (_settings.Clouds.Enabled)
            {
                ApplyCloudSettings(daylightFactor);
            }

            if (_settings.Rain.Enabled)
            {
                ApplyRainSettings(daylightFactor, deltaTime);
            }

            if (_settings.Sun.Enabled)
            {
                ApplySunSettings(daylightFactor, deltaTime);
            }
        }

        private static void ApplyFogSettings(float daylightFactor, float deltaTime)
        {
            float targetDensity = Mathf.Lerp(_settings.Fog.NightIntensity, _settings.Fog.DayIntensity, daylightFactor);
            Color targetColor = Color.Lerp(_settings.Fog.NightColor, _settings.Fog.DayColor, daylightFactor);

            _smoothedFogDensity = Mathf.Lerp(_smoothedFogDensity <= 0f ? targetDensity : _smoothedFogDensity, targetDensity, deltaTime * _settings.TransitionSpeed);
            _smoothedFogColor = Color.Lerp(_smoothedFogColor == default ? targetColor : _smoothedFogColor, targetColor, deltaTime * _settings.TransitionSpeed);

            RenderSettings.fog = true;
            RenderSettings.fogDensity = Mathf.Max(0f, _smoothedFogDensity);
            RenderSettings.fogColor = _smoothedFogColor;
        }

        private static void ApplyCloudSettings(float daylightFactor)
        {
            float targetDensity = Mathf.Lerp(_settings.Clouds.NightDensity, _settings.Clouds.DayDensity, daylightFactor);

            Shader.SetGlobalFloat(CloudDensityShaderId, Mathf.Clamp01(targetDensity));

            var weatherManager = GameManager.Instance?.WeatherManager;
            if (weatherManager != null)
            {
                SetFieldIfExists(weatherManager, "cloudOpacity", targetDensity);
                SetPropertyIfExists(weatherManager, "CloudOpacity", targetDensity);
            }
        }

        private static void ApplyRainSettings(float daylightFactor, float deltaTime)
        {
            float targetIntensity = Mathf.Lerp(_settings.Rain.NightIntensity, _settings.Rain.DayIntensity, daylightFactor);
            Shader.SetGlobalFloat(RainOpacityShaderId, Mathf.Clamp01(_settings.Rain.Opacity));

            var weatherManager = GameManager.Instance?.WeatherManager;
            if (weatherManager == null)
            {
                return;
            }

            SetFieldIfExists(weatherManager, "precipitationStrength", targetIntensity);
            SetPropertyIfExists(weatherManager, "PrecipitationStrength", targetIntensity);
            SetPropertyIfExists(weatherManager, "RainStrength", targetIntensity);

            var precipitationController = GetFieldValue<object>(weatherManager, "precipitationController");
            if (precipitationController != null)
            {
                SetPropertyIfExists(precipitationController, "Opacity", _settings.Rain.Opacity);
                SetFieldIfExists(precipitationController, "opacity", _settings.Rain.Opacity);
            }
        }

        private static void ApplySunSettings(float daylightFactor, float deltaTime)
        {
            Color dawnToDay = Color.Lerp(_settings.Sun.DawnColor, _settings.Sun.DayColor, Mathf.SmoothStep(0f, 0.5f, daylightFactor));
            Color duskToNight = Color.Lerp(_settings.Sun.DuskColor, _settings.Sun.NightColor, Mathf.SmoothStep(0f, 0.5f, 1f - daylightFactor));
            Color targetAmbient = Color.Lerp(dawnToDay, duskToNight, 0.5f);

            _smoothedAmbientColor = Color.Lerp(_smoothedAmbientColor == default ? targetAmbient : _smoothedAmbientColor, targetAmbient, deltaTime * _settings.TransitionSpeed);
            RenderSettings.ambientLight = _smoothedAmbientColor;

            var skyManager = GameManager.Instance?.SkyManager;
            if (skyManager == null)
            {
                return;
            }

            var sunColor = Color.Lerp(_settings.Sun.DawnColor, _settings.Sun.DuskColor, daylightFactor);
            SetFieldIfExists(skyManager, "dawnColor", _settings.Sun.DawnColor);
            SetFieldIfExists(skyManager, "duskColor", _settings.Sun.DuskColor);
            SetFieldIfExists(skyManager, "dayColor", _settings.Sun.DayColor);
            SetFieldIfExists(skyManager, "nightColor", _settings.Sun.NightColor);
            SetPropertyIfExists(skyManager, "SunColor", sunColor);
        }

        private static float GetNormalizedTime(World world)
        {
            if (world == null)
            {
                return 0f;
            }

            float worldTime = Convert.ToSingle(world.worldTime);
            float ticksPerDay = DefaultDayLength;

            var dayLengthField = world.GetType().GetField("ticksPerDay", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (dayLengthField != null && dayLengthField.FieldType == typeof(int))
            {
                ticksPerDay = Convert.ToSingle((int)dayLengthField.GetValue(world));
            }

            return Mathf.Repeat(worldTime, ticksPerDay) / Mathf.Max(1f, ticksPerDay);
        }

        private static float EvaluateDaylightFactor(float normalizedTime)
        {
            if (_settings.Cycle == null)
            {
                return normalizedTime;
            }

            float dawnStart = _settings.Cycle.DawnStart;
            float dayStart = _settings.Cycle.DayStart;
            float duskStart = _settings.Cycle.DuskStart;
            float nightStart = _settings.Cycle.NightStart;

            if (normalizedTime < dawnStart)
            {
                return 0f;
            }

            if (normalizedTime < dayStart)
            {
                return Mathf.InverseLerp(dawnStart, dayStart, normalizedTime);
            }

            if (normalizedTime < duskStart)
            {
                return 1f;
            }

            if (normalizedTime < nightStart)
            {
                return 1f - Mathf.InverseLerp(duskStart, nightStart, normalizedTime);
            }

            return 0f;
        }

        private static void LoadConfiguration()
        {
            try
            {
                string configPath = ResolveConfigPath();
                if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
                {
                    Debug.LogWarning("[ClimaVisual] Config/Visuals.xml no encontrado. Usando valores por defecto.");
                    _settings = VisualSettings.CreateDefault();
                    return;
                }

                var document = new XmlDocument();
                document.Load(configPath);

                var root = document.SelectSingleNode("/visuals");
                if (root == null)
                {
                    Debug.LogWarning("[ClimaVisual] Nodo raíz <visuals> no encontrado. Se mantienen los valores por defecto.");
                    _settings = VisualSettings.CreateDefault();
                    return;
                }

                var settings = VisualSettings.CreateDefault();
                settings.Enabled = ParseBoolAttribute(root, "enabled", true);

                var cycleNode = root.SelectSingleNode("cycle");
                if (cycleNode != null)
                {
                    settings.Cycle = new VisualCycle
                    {
                        TransitionSpeed = ParseFloatAttribute(cycleNode, "transitionSpeed", settings.TransitionSpeed),
                        DawnStart = ParseFloatAttribute(cycleNode, "dawnStart", settings.Cycle.DawnStart),
                        DayStart = ParseFloatAttribute(cycleNode, "dayStart", settings.Cycle.DayStart),
                        DuskStart = ParseFloatAttribute(cycleNode, "duskStart", settings.Cycle.DuskStart),
                        NightStart = ParseFloatAttribute(cycleNode, "nightStart", settings.Cycle.NightStart)
                    };
                    settings.TransitionSpeed = settings.Cycle.TransitionSpeed;
                }

                settings.Fog = ParseFogSettings(root.SelectSingleNode("fog"), settings.Fog);
                settings.Clouds = ParseCloudSettings(root.SelectSingleNode("clouds"), settings.Clouds);
                settings.Rain = ParseRainSettings(root.SelectSingleNode("rain"), settings.Rain);
                settings.Sun = ParseSunSettings(root.SelectSingleNode("sun"), settings.Sun);

                _settings = settings;
                Debug.Log("[ClimaVisual] Configuración cargada correctamente.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ClimaVisual] Error al cargar la configuración: {ex}");
                _settings = VisualSettings.CreateDefault();
            }
        }

        private static void SetupWatcher()
        {
            try
            {
                string configPath = ResolveConfigPath();
                if (string.IsNullOrEmpty(configPath))
                {
                    return;
                }

                var directory = Path.GetDirectoryName(configPath);
                if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                {
                    return;
                }

                _watcher?.Dispose();

                _watcher = new FileSystemWatcher(directory, "Visuals.xml")
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.Attributes | NotifyFilters.Security,
                    EnableRaisingEvents = true,
                    IncludeSubdirectories = false
                };

                _watcher.Changed += (_, _) => RequestReload();
                _watcher.Created += (_, _) => RequestReload();
                _watcher.Renamed += (_, _) => RequestReload();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ClimaVisual] No se pudo iniciar el watcher de configuración: {ex.Message}");
            }
        }

        private static void RequestReload()
        {
            _pendingReload = true;
            _reloadRequestedAt = DateTime.UtcNow;
        }

        private static void TryReloadIfRequested()
        {
            if (!_pendingReload)
            {
                return;
            }

            var elapsed = DateTime.UtcNow - _reloadRequestedAt;
            if (elapsed.TotalSeconds < 0.25f)
            {
                return;
            }

            _pendingReload = false;
            LoadConfiguration();
        }

        private static FogSettings ParseFogSettings(XmlNode node, FogSettings fallback)
        {
            if (node == null)
            {
                return fallback;
            }

            return new FogSettings
            {
                Enabled = ParseBoolAttribute(node, "enabled", fallback.Enabled),
                DayIntensity = ParseFloatNode(node.SelectSingleNode("FogIntensityDay"), fallback.DayIntensity),
                NightIntensity = ParseFloatNode(node.SelectSingleNode("FogIntensityNight"), fallback.NightIntensity),
                DayColor = ParseColorNode(node.SelectSingleNode("DayColor"), fallback.DayColor),
                NightColor = ParseColorNode(node.SelectSingleNode("NightColor"), fallback.NightColor)
            };
        }

        private static CloudSettings ParseCloudSettings(XmlNode node, CloudSettings fallback)
        {
            if (node == null)
            {
                return fallback;
            }

            return new CloudSettings
            {
                Enabled = ParseBoolAttribute(node, "enabled", fallback.Enabled),
                DayDensity = ParseFloatNode(node.SelectSingleNode("CloudDensityDay"), fallback.DayDensity),
                NightDensity = ParseFloatNode(node.SelectSingleNode("CloudDensityNight"), fallback.NightDensity)
            };
        }

        private static RainSettings ParseRainSettings(XmlNode node, RainSettings fallback)
        {
            if (node == null)
            {
                return fallback;
            }

            return new RainSettings
            {
                Enabled = ParseBoolAttribute(node, "enabled", fallback.Enabled),
                Opacity = ParseFloatNode(node.SelectSingleNode("RainOpacity"), fallback.Opacity),
                DayIntensity = ParseFloatNode(node.SelectSingleNode("RainIntensityDay"), fallback.DayIntensity),
                NightIntensity = ParseFloatNode(node.SelectSingleNode("RainIntensityNight"), fallback.NightIntensity)
            };
        }

        private static SunSettings ParseSunSettings(XmlNode node, SunSettings fallback)
        {
            if (node == null)
            {
                return fallback;
            }

            return new SunSettings
            {
                Enabled = ParseBoolAttribute(node, "enabled", fallback.Enabled),
                DawnColor = ParseColorNode(node.SelectSingleNode("DawnColor"), fallback.DawnColor),
                DayColor = ParseColorNode(node.SelectSingleNode("DayColor"), fallback.DayColor),
                DuskColor = ParseColorNode(node.SelectSingleNode("DuskColor"), fallback.DuskColor),
                NightColor = ParseColorNode(node.SelectSingleNode("NightColor"), fallback.NightColor)
            };
        }

        private static string ResolveConfigPath()
        {
            var candidates = new List<string>();

            string currentDirectory = Directory.GetCurrentDirectory();
            candidates.Add(Path.Combine(currentDirectory, "Mods", "ClimaVisual", "Config", "Visuals.xml"));
            candidates.Add(Path.Combine(currentDirectory, "Data", "Mods", "ClimaVisual", "Config", "Visuals.xml"));

            try
            {
                var mod = ModManager.GetMod("ClimaVisual");
                if (mod != null)
                {
                    candidates.Add(Path.Combine(mod.Path, "Config", "Visuals.xml"));
                }
            }
            catch
            {
                // Ignored: ModManager might not be initialised in editor tools.
            }

            foreach (var candidate in candidates.Where(File.Exists))
            {
                return candidate;
            }

            try
            {
                var fallback = Directory.GetFiles(currentDirectory, "Visuals.xml", SearchOption.AllDirectories)
                    .FirstOrDefault(p => p.Replace('\\', '/').EndsWith("ClimaVisual/Config/Visuals.xml", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(fallback))
                {
                    return fallback;
                }
            }
            catch
            {
                // ignored
            }

            return null;
        }

        private static bool ParseBoolAttribute(XmlNode node, string attributeName, bool fallback)
        {
            if (node?.Attributes?[attributeName] == null)
            {
                return fallback;
            }

            return bool.TryParse(node.Attributes[attributeName].Value, out var result) ? result : fallback;
        }

        private static float ParseFloatAttribute(XmlNode node, string attributeName, float fallback)
        {
            if (node?.Attributes?[attributeName] == null)
            {
                return fallback;
            }

            return ParseFloat(node.Attributes[attributeName].Value, fallback);
        }

        private static float ParseFloatNode(XmlNode node, float fallback)
        {
            if (node?.Attributes?["value"] == null)
            {
                return fallback;
            }

            return ParseFloat(node.Attributes["value"].Value, fallback);
        }

        private static float ParseFloat(string value, float fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : fallback;
        }

        private static Color ParseColorNode(XmlNode node, Color fallback)
        {
            if (node?.Attributes?["value"] == null)
            {
                return fallback;
            }

            var parts = node.Attributes["value"].Value.Split(',');
            if (parts.Length < 3)
            {
                return fallback;
            }

            float r = ParseFloat(parts[0], fallback.r);
            float g = ParseFloat(parts[1], fallback.g);
            float b = ParseFloat(parts[2], fallback.b);
            float a = parts.Length > 3 ? ParseFloat(parts[3], fallback.a) : fallback.a;

            return new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b), Mathf.Clamp01(a));
        }

        private static void SetFieldIfExists(object target, string fieldName, object value)
        {
            if (target == null)
            {
                return;
            }

            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
            {
                return;
            }

            if (value == null)
            {
                field.SetValue(target, null);
                return;
            }

            if (field.FieldType.IsInstanceOfType(value))
            {
                field.SetValue(target, value);
                return;
            }

            try
            {
                var converted = Convert.ChangeType(value, field.FieldType, CultureInfo.InvariantCulture);
                field.SetValue(target, converted);
            }
            catch
            {
                // ignored
            }
        }

        private static void SetPropertyIfExists(object target, string propertyName, object value)
        {
            if (target == null)
            {
                return;
            }

            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanWrite)
            {
                if (value == null || property.PropertyType.IsInstanceOfType(value))
                {
                    property.SetValue(target, value);
                }
                else
                {
                    try
                    {
                        var converted = Convert.ChangeType(value, property.PropertyType, CultureInfo.InvariantCulture);
                        property.SetValue(target, converted);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
        }

        private static T GetFieldValue<T>(object target, string fieldName)
        {
            if (target == null)
            {
                return default;
            }

            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
            {
                return default;
            }

            if (typeof(T).IsAssignableFrom(field.FieldType))
            {
                return (T)field.GetValue(target);
            }

            try
            {
                object value = field.GetValue(target);
                if (value == null)
                {
                    return default;
                }

                return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
            }
            catch
            {
                return default;
            }
        }
    }

    internal sealed class VisualSettings
    {
        internal bool Enabled { get; set; } = true;
        internal float TransitionSpeed { get; set; } = 2.5f;
        internal VisualCycle Cycle { get; set; } = new();
        internal FogSettings Fog { get; set; } = new();
        internal CloudSettings Clouds { get; set; } = new();
        internal RainSettings Rain { get; set; } = new();
        internal SunSettings Sun { get; set; } = new();

        internal static VisualSettings CreateDefault()
        {
            return new VisualSettings
            {
                Enabled = true,
                TransitionSpeed = 2.5f,
                Cycle = new VisualCycle
                {
                    DawnStart = 0.18f,
                    DayStart = 0.24f,
                    DuskStart = 0.70f,
                    NightStart = 0.80f,
                    TransitionSpeed = 2.5f
                },
                Fog = new FogSettings
                {
                    Enabled = true,
                    DayIntensity = 0.0022f,
                    NightIntensity = 0.0055f,
                    DayColor = new Color(0.78f, 0.81f, 0.86f),
                    NightColor = new Color(0.22f, 0.26f, 0.33f)
                },
                Clouds = new CloudSettings
                {
                    Enabled = true,
                    DayDensity = 0.35f,
                    NightDensity = 0.62f
                },
                Rain = new RainSettings
                {
                    Enabled = true,
                    Opacity = 0.45f,
                    DayIntensity = 0.15f,
                    NightIntensity = 0.35f
                },
                Sun = new SunSettings
                {
                    Enabled = true,
                    DawnColor = new Color(0.94f, 0.58f, 0.36f),
                    DayColor = new Color(0.83f, 0.87f, 0.92f),
                    DuskColor = new Color(0.68f, 0.45f, 0.78f),
                    NightColor = new Color(0.18f, 0.22f, 0.32f)
                }
            };
        }
    }

    internal sealed class VisualCycle
    {
        internal float TransitionSpeed { get; set; } = 2.5f;
        internal float DawnStart { get; set; } = 0.18f;
        internal float DayStart { get; set; } = 0.24f;
        internal float DuskStart { get; set; } = 0.70f;
        internal float NightStart { get; set; } = 0.80f;
    }

    internal sealed class FogSettings
    {
        internal bool Enabled { get; set; } = true;
        internal float DayIntensity { get; set; } = 0.0022f;
        internal float NightIntensity { get; set; } = 0.0055f;
        internal Color DayColor { get; set; } = new(0.78f, 0.81f, 0.86f);
        internal Color NightColor { get; set; } = new(0.22f, 0.26f, 0.33f);
    }

    internal sealed class CloudSettings
    {
        internal bool Enabled { get; set; } = true;
        internal float DayDensity { get; set; } = 0.35f;
        internal float NightDensity { get; set; } = 0.62f;
    }

    internal sealed class RainSettings
    {
        internal bool Enabled { get; set; } = true;
        internal float Opacity { get; set; } = 0.45f;
        internal float DayIntensity { get; set; } = 0.15f;
        internal float NightIntensity { get; set; } = 0.35f;
    }

    internal sealed class SunSettings
    {
        internal bool Enabled { get; set; } = true;
        internal Color DawnColor { get; set; } = new(0.94f, 0.58f, 0.36f);
        internal Color DayColor { get; set; } = new(0.83f, 0.87f, 0.92f);
        internal Color DuskColor { get; set; } = new(0.68f, 0.45f, 0.78f);
        internal Color NightColor { get; set; } = new(0.18f, 0.22f, 0.32f);
    }
}
