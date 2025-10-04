using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using HarmonyLib;
using UnityEngine;

namespace AmbientSounds
{
    /// <summary>
    /// Harmony bootstrapper que inicializa el gestor de sonidos ambiente y actualiza el bucle cada frame.
    /// </summary>
    [HarmonyPatch]
    public static class AmbientSoundsBootstrap
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

            AmbientSoundManager.Initialize();
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

            AmbientSoundManager.Tick();
        }
    }

    /// <summary>
    /// Gestor principal encargado de leer la configuración, evaluar el contexto del jugador y reproducir el bucle ambiente.
    /// </summary>
    internal static class AmbientSoundManager
    {
        private enum FadeState
        {
            Idle,
            FadingOut,
            Switching,
            FadingIn
        }

        private static AmbientSoundSettings _settings = AmbientSoundSettings.CreateDefault();
        private static FileSystemWatcher _watcher;
        private static bool _pendingReload;
        private static DateTime _reloadRequestedAt = DateTime.MinValue;

        private static GameObject _audioRoot;
        private static AudioSource _audioSource;
        private static AmbientSoundDefinition _activeDefinition;
        private static AmbientSoundDefinition _pendingDefinition;
        private static FadeState _fadeState = FadeState.Idle;
        private static float _currentVolume;

        internal static void Initialize()
        {
            LoadConfiguration();
            SetupWatcher();
        }

        internal static void Tick()
        {
            if (_settings == null || !_settings.Enabled)
            {
                StopAmbientLoop(Time.deltaTime <= 0f ? 0.016f : Time.deltaTime);
                return;
            }

            TryReloadIfRequested();

            var gameManager = GameManager.Instance;
            var world = gameManager?.World;
            var player = GetPrimaryPlayer(world);

            if (world == null || player == null)
            {
                StopAmbientLoop(Time.deltaTime <= 0f ? 0.016f : Time.deltaTime);
                return;
            }

            string biome = DetermineBiome(world, player);
            string condition = DetermineCondition(world, player);
            float deltaTime = Time.deltaTime <= 0f ? 0.016f : Time.deltaTime;

            var desiredDefinition = SelectDefinition(biome, condition);
            if (!DefinitionsEqual(desiredDefinition, _activeDefinition) || (_pendingDefinition != null && !DefinitionsEqual(_pendingDefinition, desiredDefinition)))
            {
                _pendingDefinition = desiredDefinition;
                if (_fadeState == FadeState.FadingIn || _fadeState == FadeState.Idle)
                {
                    _fadeState = _audioSource != null && _audioSource.isPlaying ? FadeState.FadingOut : FadeState.Switching;
                }
            }

            UpdateFade(deltaTime);
            UpdateSpatialBlend(player);
        }

        private static void StopAmbientLoop(float deltaTime)
        {
            if (_audioSource == null || !_audioSource.isPlaying)
            {
                return;
            }

            _pendingDefinition = null;
            _fadeState = FadeState.FadingOut;
            UpdateFade(deltaTime);
        }

        private static void UpdateFade(float deltaTime)
        {
            EnsureAudioSource();

            switch (_fadeState)
            {
                case FadeState.Idle:
                {
                    if (_activeDefinition == null || !_audioSource.isPlaying)
                    {
                        _currentVolume = 0f;
                        _audioSource.volume = 0f;
                    }
                    break;
                }

                case FadeState.FadingOut:
                {
                    float fadeOutDuration = Mathf.Max(0.05f, _activeDefinition?.FadeOutSeconds ?? _settings.DefaultFadeOutSeconds);
                    float step = deltaTime / fadeOutDuration;
                    _currentVolume = Mathf.MoveTowards(_currentVolume, 0f, step);
                    _audioSource.volume = _currentVolume;

                    if (Mathf.Approximately(_currentVolume, 0f))
                    {
                        _audioSource.Stop();
                        _activeDefinition = null;
                        _fadeState = FadeState.Switching;
                    }

                    break;
                }

                case FadeState.Switching:
                {
                    if (_pendingDefinition == null)
                    {
                        _fadeState = FadeState.Idle;
                        break;
                    }

                    if (_pendingDefinition.IsSilence)
                    {
                        _activeDefinition = _pendingDefinition;
                        _pendingDefinition = null;
                        _fadeState = FadeState.Idle;
                        break;
                    }

                    if (!StartDefinition(_pendingDefinition))
                    {
                        _pendingDefinition = null;
                        _fadeState = FadeState.Idle;
                        break;
                    }

                    _pendingDefinition = null;
                    _fadeState = FadeState.FadingIn;
                    break;
                }

                case FadeState.FadingIn:
                {
                    if (_activeDefinition == null)
                    {
                        _fadeState = FadeState.Idle;
                        break;
                    }

                    float fadeInDuration = Mathf.Max(0.05f, _activeDefinition.FadeInSeconds > 0f ? _activeDefinition.FadeInSeconds : _settings.DefaultFadeInSeconds);
                    float targetVolume = Mathf.Clamp01(_settings.MasterVolume * _activeDefinition.Volume);
                    float step = targetVolume <= 0f ? 1f : (targetVolume / fadeInDuration);
                    _currentVolume = Mathf.MoveTowards(_currentVolume, targetVolume, step * deltaTime);
                    _audioSource.volume = _currentVolume;

                    if (Mathf.Approximately(_currentVolume, targetVolume))
                    {
                        _fadeState = FadeState.Idle;
                    }

                    break;
                }
            }
        }

        private static void UpdateSpatialBlend(EntityPlayerLocal player)
        {
            if (_audioSource == null || _activeDefinition == null)
            {
                return;
            }

            float radius = _activeDefinition.Radius > 0f ? _activeDefinition.Radius : _settings.DefaultRadius;
            if (radius <= 0f)
            {
                _audioSource.spatialBlend = 0f;
                return;
            }

            _audioSource.spatialBlend = 1f;
            _audioSource.rolloffMode = AudioRolloffMode.Linear;
            _audioSource.maxDistance = Mathf.Max(radius, 1f);
            _audioSource.transform.position = player != null ? player.position : Vector3.zero;
        }

        private static bool StartDefinition(AmbientSoundDefinition definition)
        {
            EnsureAudioSource();

            if (definition == null || definition.IsSilence)
            {
                return false;
            }

            var clip = LoadClip(definition.Clip);
            if (clip == null)
            {
                Debug.LogWarning($"[AmbientSounds] Clip '{definition.Clip}' no encontrado. Se omite.");
                return false;
            }

            _audioSource.clip = clip;
            _audioSource.loop = true;
            _audioSource.priority = CalculateUnityPriority(definition.Priority);
            _audioSource.volume = 0f;
            _currentVolume = 0f;
            _activeDefinition = definition;
            _audioSource.Play();

            return true;
        }

        private static void EnsureAudioSource()
        {
            if (_audioSource != null)
            {
                return;
            }

            _audioRoot = new GameObject("AmbientSounds_AudioSource")
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            UnityEngine.Object.DontDestroyOnLoad(_audioRoot);

            _audioSource = _audioRoot.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.loop = true;
            _audioSource.spatialBlend = 0f;
            _audioSource.volume = 0f;
        }

        // Convierte la prioridad abstracta (0-1) en el rango que Unity utiliza internamente (0-256).
        private static int CalculateUnityPriority(float definitionPriority)
        {
            float combined = Mathf.Clamp01(_settings.Priority) + Mathf.Clamp01(definitionPriority);
            float normalized = Mathf.Clamp01(combined * 0.5f);
            return Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(200f, 64f, normalized)), 0, 256);
        }

        private static AmbientSoundDefinition SelectDefinition(string biome, string condition)
        {
            if (_settings?.Sounds == null || _settings.Sounds.Count == 0)
            {
                return null;
            }

            var exactMatches = _settings.Sounds
                .Where(s => s.Matches(biome, condition))
                .OrderByDescending(s => s.Priority)
                .ThenByDescending(s => s.Volume)
                .ToList();

            if (exactMatches.Count > 0)
            {
                return exactMatches[0];
            }

            var fallback = _settings.Sounds
                .Where(s => s.Matches(biome, "default"))
                .OrderByDescending(s => s.Priority)
                .ThenByDescending(s => s.Volume)
                .FirstOrDefault();

            return fallback ?? _settings.Sounds
                .Where(s => s.Matches("any", condition))
                .OrderByDescending(s => s.Priority)
                .ThenByDescending(s => s.Volume)
                .FirstOrDefault();
        }

        private static bool DefinitionsEqual(AmbientSoundDefinition a, AmbientSoundDefinition b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a == null || b == null)
            {
                return false;
            }

            return string.Equals(a.Biome, b.Biome, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(a.Condition, b.Condition, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(a.Clip, b.Clip, StringComparison.Ordinal)
                   && Mathf.Approximately(a.Volume, b.Volume);
        }

        private static void LoadConfiguration()
        {
            try
            {
                string configPath = ResolveConfigPath();
                if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
                {
                    Debug.LogWarning("[AmbientSounds] Config/Sounds.xml no encontrado. Se usarán valores por defecto.");
                    _settings = AmbientSoundSettings.CreateDefault();
                    return;
                }

                var document = new XmlDocument();
                document.Load(configPath);

                var root = document.SelectSingleNode("/Sounds");
                if (root == null)
                {
                    Debug.LogWarning("[AmbientSounds] Nodo raíz <Sounds> no encontrado. Se mantienen los valores por defecto.");
                    _settings = AmbientSoundSettings.CreateDefault();
                    return;
                }

                var settings = AmbientSoundSettings.CreateDefault();
                settings.Enabled = ParseBoolAttribute(root, "enabled", settings.Enabled);
                settings.MasterVolume = ParseFloatAttribute(root, "masterVolume", settings.MasterVolume);
                settings.Priority = ParseFloatAttribute(root, "priority", settings.Priority);

                var defaultsNode = root.SelectSingleNode("defaults");
                if (defaultsNode != null)
                {
                    settings.DefaultFadeInSeconds = ParseFloatAttribute(defaultsNode, "fadeIn", settings.DefaultFadeInSeconds);
                    settings.DefaultFadeOutSeconds = ParseFloatAttribute(defaultsNode, "fadeOut", settings.DefaultFadeOutSeconds);
                    settings.DefaultRadius = ParseFloatAttribute(defaultsNode, "radius", settings.DefaultRadius);
                }

                var soundNodes = root.SelectNodes("ambient");
                settings.Sounds = new List<AmbientSoundDefinition>();

                if (soundNodes != null)
                {
                    foreach (XmlNode node in soundNodes)
                    {
                        var definition = new AmbientSoundDefinition
                        {
                            Biome = ParseStringAttribute(node, "biome", "any"),
                            Condition = ParseStringAttribute(node, "condition", "default"),
                            Clip = ParseStringAttribute(node, "clip", string.Empty),
                            Volume = ParseFloatAttribute(node, "volume", 1f),
                            FadeInSeconds = ParseFloatAttribute(node, "fadeIn", -1f),
                            FadeOutSeconds = ParseFloatAttribute(node, "fadeOut", -1f),
                            Radius = ParseFloatAttribute(node, "radius", -1f),
                            Priority = ParseFloatAttribute(node, "priority", 0f)
                        };

                        settings.Sounds.Add(definition);
                    }
                }

                _settings = settings;
                Debug.Log("[AmbientSounds] Configuración de sonidos cargada correctamente.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AmbientSounds] Error al cargar Config/Sounds.xml: {ex}");
                _settings = AmbientSoundSettings.CreateDefault();
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

                _watcher = new FileSystemWatcher(directory, "Sounds.xml")
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                    EnableRaisingEvents = true,
                    IncludeSubdirectories = false
                };

                _watcher.Changed += (_, _) => RequestReload();
                _watcher.Created += (_, _) => RequestReload();
                _watcher.Renamed += (_, _) => RequestReload();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AmbientSounds] No se pudo iniciar el watcher de configuración: {ex.Message}");
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

            if ((DateTime.UtcNow - _reloadRequestedAt).TotalSeconds < 0.25f)
            {
                return;
            }

            _pendingReload = false;
            LoadConfiguration();
        }

        private static string ResolveConfigPath()
        {
            var candidates = new List<string>();
            string currentDirectory = Directory.GetCurrentDirectory();

            candidates.Add(Path.Combine(currentDirectory, "Mods", "AmbientSounds", "Config", "Sounds.xml"));
            candidates.Add(Path.Combine(currentDirectory, "Data", "Mods", "AmbientSounds", "Config", "Sounds.xml"));

            try
            {
                var mod = ModManager.GetMod("AmbientSounds");
                if (mod != null)
                {
                    candidates.Add(Path.Combine(mod.Path, "Config", "Sounds.xml"));
                }
            }
            catch
            {
                // Ignorado: ModManager puede no estar inicializado en algunas herramientas.
            }

            foreach (var candidate in candidates.Where(File.Exists))
            {
                return candidate;
            }

            try
            {
                var fallback = Directory.GetFiles(currentDirectory, "Sounds.xml", SearchOption.AllDirectories)
                    .FirstOrDefault(p => p.Replace('\\', '/').EndsWith("AmbientSounds/Config/Sounds.xml", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(fallback))
                {
                    return fallback;
                }
            }
            catch
            {
                // Ignorado.
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

        private static string ParseStringAttribute(XmlNode node, string attributeName, string fallback)
        {
            if (node?.Attributes?[attributeName] == null)
            {
                return fallback;
            }

            return node.Attributes[attributeName].Value;
        }

        private static float ParseFloat(string value, float fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : fallback;
        }

        private static EntityPlayerLocal GetPrimaryPlayer(World world)
        {
            if (world == null)
            {
                return null;
            }

            try
            {
                return world.GetPrimaryPlayer();
            }
            catch
            {
                try
                {
                    var method = world.GetType().GetMethod("GetPrimaryPlayer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                    if (method != null)
                    {
                        return method.Invoke(world, null) as EntityPlayerLocal;
                    }
                }
                catch
                {
                    // Ignorado.
                }
            }

            return null;
        }

        private static string DetermineBiome(World world, EntityPlayerLocal player)
        {
            if (world == null || player == null)
            {
                return "any";
            }

            try
            {
                var biomeMapProperty = world.GetType().GetProperty("BiomeMap", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var biomeMap = biomeMapProperty?.GetValue(world);
                if (biomeMap == null)
                {
                    return "any";
                }

                Vector3 position = player.position;
                int x = Mathf.FloorToInt(position.x);
                int y = Mathf.FloorToInt(position.y);
                int z = Mathf.FloorToInt(position.z);

                var method = biomeMap.GetType().GetMethod("GetBiomeAt", new[] { typeof(int), typeof(int), typeof(int) })
                             ?? biomeMap.GetType().GetMethod("GetBiome", new[] { typeof(int), typeof(int), typeof(int) });

                object biome = null;
                if (method != null)
                {
                    biome = method.Invoke(biomeMap, new object[] { x, y, z });
                }
                else
                {
                    var vector3iType = AccessTools.TypeByName("Vector3i");
                    if (vector3iType != null)
                    {
                        object vector = Activator.CreateInstance(vector3iType, x, y, z);
                        var getBiomeAt = biomeMap.GetType().GetMethod("GetBiomeAt", new[] { vector3iType });
                        if (getBiomeAt != null)
                        {
                            biome = getBiomeAt.Invoke(biomeMap, new[] { vector });
                        }
                    }
                }

                if (biome == null)
                {
                    return "any";
                }

                var nameField = biome.GetType().GetField("m_sBiomeName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (nameField != null)
                {
                    var value = nameField.GetValue(biome) as string;
                    if (!string.IsNullOrEmpty(value))
                    {
                        return value.ToLowerInvariant();
                    }
                }

                var nameProperty = biome.GetType().GetProperty("m_sBiomeName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                   ?? biome.GetType().GetProperty("biomeName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (nameProperty != null)
                {
                    var value = nameProperty.GetValue(biome) as string;
                    if (!string.IsNullOrEmpty(value))
                    {
                        return value.ToLowerInvariant();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AmbientSounds] No se pudo determinar el bioma: {ex.Message}");
            }

            return "any";
        }

        private static string DetermineCondition(World world, EntityPlayerLocal player)
        {
            if (world == null || player == null)
            {
                return "default";
            }

            bool underground = IsPlayerUnderground(world, player);
            if (underground)
            {
                return "cave";
            }

            var weatherManager = GameManager.Instance?.WeatherManager;
            float rain = GetFloatProperty(weatherManager, "RainStrength");
            float storm = GetFloatProperty(weatherManager, "StormStrength");
            float wind = GetFloatProperty(weatherManager, "WindStrength");

            if (storm >= _settings.StormThreshold)
            {
                return "storm";
            }

            if (rain >= _settings.RainThreshold)
            {
                return "rain";
            }

            if (wind >= _settings.WindThreshold)
            {
                return "wind";
            }

            float normalizedTime = GetNormalizedTime(world);
            if (normalizedTime >= 0.8f || normalizedTime <= 0.18f)
            {
                return "night";
            }

            return "default";
        }

        private static bool IsPlayerUnderground(World world, EntityPlayerLocal player)
        {
            try
            {
                Vector3 position = player.position;
                int x = Mathf.FloorToInt(position.x);
                int z = Mathf.FloorToInt(position.z);

                var method = world.GetType().GetMethod("GetTerrainHeight", new[] { typeof(int), typeof(int) })
                             ?? world.GetType().GetMethod("GetHeightAt", new[] { typeof(int), typeof(int) })
                             ?? world.GetType().GetMethod("GetHeight", new[] { typeof(int), typeof(int) });

                if (method != null)
                {
                    float terrainHeight = Convert.ToSingle(method.Invoke(world, new object[] { x, z }));
                    return position.y < terrainHeight - _settings.CaveDepthOffset;
                }
            }
            catch
            {
                // Ignorado: si falla, se asume exterior.
            }

            return false;
        }

        private static float GetNormalizedTime(World world)
        {
            if (world == null)
            {
                return 0f;
            }

            float worldTime = Convert.ToSingle(world.worldTime);
            float ticksPerDay = AmbientSoundSettings.DefaultDayLength;

            var dayLengthField = world.GetType().GetField("ticksPerDay", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (dayLengthField != null && dayLengthField.FieldType == typeof(int))
            {
                ticksPerDay = Convert.ToSingle((int)dayLengthField.GetValue(world));
            }

            return Mathf.Repeat(worldTime, ticksPerDay) / Mathf.Max(1f, ticksPerDay);
        }

        private static float GetFloatProperty(object target, string propertyName)
        {
            if (target == null)
            {
                return 0f;
            }

            try
            {
                var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null && (property.PropertyType == typeof(float) || property.PropertyType == typeof(double)))
                {
                    return Convert.ToSingle(property.GetValue(target));
                }

                var field = target.GetType().GetField(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null && (field.FieldType == typeof(float) || field.FieldType == typeof(double)))
                {
                    return Convert.ToSingle(field.GetValue(target));
                }
            }
            catch
            {
                // Ignorado.
            }

            return 0f;
        }

        private static AudioClip LoadClip(string clipName)
        {
            if (string.IsNullOrWhiteSpace(clipName))
            {
                return null;
            }

            try
            {
                var audioManagerType = AccessTools.TypeByName("AudioManager");
                if (audioManagerType != null)
                {
                    var instanceProperty = audioManagerType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    var instance = instanceProperty?.GetValue(null);
                    if (instance != null)
                    {
                        foreach (var methodName in new[] { "GetAudioClip", "GetClip", "LoadClip" })
                        {
                            var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(string) }, null);
                            if (method != null)
                            {
                                var result = method.Invoke(instance, new object[] { clipName }) as AudioClip;
                                if (result != null)
                                {
                                    return result;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AmbientSounds] Error al consultar AudioManager: {ex.Message}");
            }

            var resourcesClip = Resources.Load<AudioClip>(clipName);
            if (resourcesClip == null)
            {
                Debug.LogWarning($"[AmbientSounds] Clip '{clipName}' no encontrado en Resources.");
            }

            return resourcesClip;
        }
    }

    internal sealed class AmbientSoundSettings
    {
        internal const float DefaultDayLength = 24000f;

        // Permite activar o desactivar completamente el módulo desde Config/Sounds.xml.
        internal bool Enabled { get; set; } = true;

        // Volumen global aplicado a cualquier clip antes de la atenuación progresiva.
        internal float MasterVolume { get; set; } = 0.45f;

        // Factor de prioridad frente a otros sonidos diegéticos: se traduce al rango 0-256 de Unity.
        internal float Priority { get; set; } = 0.35f;
        internal float DefaultFadeInSeconds { get; set; } = 4f;
        internal float DefaultFadeOutSeconds { get; set; } = 6f;
        internal float DefaultRadius { get; set; } = 0f;
        internal float RainThreshold { get; set; } = 0.35f;
        internal float StormThreshold { get; set; } = 0.55f;
        internal float WindThreshold { get; set; } = 0.45f;
        internal float CaveDepthOffset { get; set; } = 4f;
        internal List<AmbientSoundDefinition> Sounds { get; set; } = new();

        internal static AmbientSoundSettings CreateDefault()
        {
            return new AmbientSoundSettings
            {
                Enabled = true,
                MasterVolume = 0.45f,
                Priority = 0.35f,
                DefaultFadeInSeconds = 4f,
                DefaultFadeOutSeconds = 6f,
                DefaultRadius = 0f,
                RainThreshold = 0.35f,
                StormThreshold = 0.55f,
                WindThreshold = 0.45f,
                CaveDepthOffset = 4f,
                Sounds = new List<AmbientSoundDefinition>
                {
                    new AmbientSoundDefinition { Biome = "forest", Condition = "default", Clip = "ambient_forest_day", Volume = 1f },
                    new AmbientSoundDefinition { Biome = "forest", Condition = "night", Clip = "ambient_forest_night", Volume = 0.85f },
                    new AmbientSoundDefinition { Biome = "desert", Condition = "default", Clip = "ambient_desert_day", Volume = 0.95f },
                    new AmbientSoundDefinition { Biome = "snow", Condition = "default", Clip = "ambient_snow_wind", Volume = 0.9f },
                    new AmbientSoundDefinition { Biome = "wasteland", Condition = "default", Clip = "ambient_wasteland_low", Volume = 0.75f },
                    new AmbientSoundDefinition { Biome = "any", Condition = "rain", Clip = "ambient_rain_light", Volume = 0.65f, FadeInSeconds = 2.5f, FadeOutSeconds = 4f },
                    new AmbientSoundDefinition { Biome = "any", Condition = "storm", Clip = "ambient_storm_heavy", Volume = 0.55f, FadeInSeconds = 1.5f, FadeOutSeconds = 3.5f },
                    new AmbientSoundDefinition { Biome = "any", Condition = "wind", Clip = "ambient_wind_gusts", Volume = 0.6f, Radius = 40f },
                    new AmbientSoundDefinition { Biome = "any", Condition = "cave", Clip = "ambient_cave_drones", Volume = 0.7f, FadeInSeconds = 3.5f, FadeOutSeconds = 5.5f },
                    new AmbientSoundDefinition { Biome = "burntforest", Condition = "default", Clip = string.Empty, Volume = 0f }
                }
            };
        }
    }

    internal sealed class AmbientSoundDefinition
    {
        internal string Biome { get; set; } = "any";
        internal string Condition { get; set; } = "default";
        internal string Clip { get; set; } = string.Empty;
        internal float Volume { get; set; } = 1f;
        internal float FadeInSeconds { get; set; } = -1f;
        internal float FadeOutSeconds { get; set; } = -1f;
        internal float Radius { get; set; } = -1f;
        internal float Priority { get; set; } = 0f;

        internal bool IsSilence => string.IsNullOrWhiteSpace(Clip) || Volume <= 0f;

        internal bool Matches(string biome, string condition)
        {
            bool biomeMatches = string.Equals(Biome, "any", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(Biome, biome, StringComparison.OrdinalIgnoreCase);

            bool conditionMatches = string.Equals(Condition, "any", StringComparison.OrdinalIgnoreCase)
                                    || string.Equals(Condition, condition, StringComparison.OrdinalIgnoreCase);

            return biomeMatches && conditionMatches;
        }
    }
}
