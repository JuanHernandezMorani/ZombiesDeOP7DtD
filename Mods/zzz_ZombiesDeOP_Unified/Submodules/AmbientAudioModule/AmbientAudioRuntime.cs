using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using ZombiesDeOPUnified.Core;
using AmbientDefinition = ZombiesDeOPUnified.Core.ModConfig.AmbientDefinition;

namespace ZombiesDeOPUnified.Submodules.AmbientAudioModule
{
    public sealed class AmbientAudioRuntime : MonoBehaviour, IModule
    {
        private enum FadeState
        {
            Idle,
            FadingOut,
            Switching,
            FadingIn
        }

        private ModConfig.AmbientAudioSettings settings;
        private GameObject audioRoot;
        private AudioSource audioSource;
        private AmbientDefinition activeDefinition;
        private AmbientDefinition pendingDefinition;
        private FadeState fadeState = FadeState.Idle;
        private float currentVolume;

        public bool IsEnabled => AmbientAudioConfig.EnableAmbientAudio;
        public string ModuleName => "Audio Ambiente";

        public void InitializeModule()
        {
            enabled = IsEnabled;
            if (!IsEnabled)
            {
                return;
            }

            ModConfig.ConfigReloaded += OnConfigReloaded;
            settings = AmbientAudioConfig.Settings;
            EnsureAudioSource();
        }

        public void Shutdown()
        {
            ModConfig.ConfigReloaded -= OnConfigReloaded;
            if (audioSource != null)
            {
                audioSource.Stop();
            }

            if (audioRoot != null)
            {
                Destroy(audioRoot);
                audioRoot = null;
                audioSource = null;
            }

            activeDefinition = null;
            pendingDefinition = null;
            fadeState = FadeState.Idle;
            enabled = false;
        }

        private void OnConfigReloaded()
        {
            settings = AmbientAudioConfig.Settings;
            pendingDefinition = null;
            fadeState = FadeState.FadingOut;
        }

        private void Update()
        {
            if (!IsEnabled || settings == null || !settings.Enabled)
            {
                StopAmbientLoop(Time.deltaTime <= 0f ? 0.016f : Time.deltaTime);
                return;
            }

            float deltaTime = Time.deltaTime <= 0f ? 0.016f : Time.deltaTime;
            var world = GameManager.Instance?.World;
            var player = world?.GetPrimaryPlayer();

            if (world == null || player == null)
            {
                StopAmbientLoop(deltaTime);
                return;
            }

            var desiredDefinition = SelectDefinition(world, player);
            if (!DefinitionsEqual(desiredDefinition, activeDefinition) ||
                (pendingDefinition != null && !DefinitionsEqual(pendingDefinition, desiredDefinition)))
            {
                pendingDefinition = desiredDefinition;
                if (fadeState == FadeState.FadingIn || fadeState == FadeState.Idle)
                {
                    fadeState = audioSource != null && audioSource.isPlaying ? FadeState.FadingOut : FadeState.Switching;
                }
            }

            UpdateFade(deltaTime);
            UpdateSpatialBlend(player);
        }

        private void StopAmbientLoop(float deltaTime)
        {
            if (audioSource == null || !audioSource.isPlaying)
            {
                return;
            }

            pendingDefinition = null;
            fadeState = FadeState.FadingOut;
            UpdateFade(deltaTime);
        }

        private void UpdateFade(float deltaTime)
        {
            EnsureAudioSource();

            switch (fadeState)
            {
                case FadeState.Idle:
                    if (activeDefinition == null || !audioSource.isPlaying)
                    {
                        currentVolume = 0f;
                        audioSource.volume = 0f;
                    }
                    break;

                case FadeState.FadingOut:
                {
                    float fadeOutDuration = Mathf.Max(0.05f, ResolveFadeOut(activeDefinition));
                    float step = deltaTime / fadeOutDuration;
                    currentVolume = Mathf.MoveTowards(currentVolume, 0f, step);
                    audioSource.volume = currentVolume * settings.MasterVolume;

                    if (Mathf.Approximately(currentVolume, 0f))
                    {
                        audioSource.Stop();
                        activeDefinition = null;
                        fadeState = FadeState.Switching;
                    }

                    break;
                }

                case FadeState.Switching:
                {
                    if (pendingDefinition == null)
                    {
                        fadeState = FadeState.Idle;
                        return;
                    }

                    ApplyDefinition(pendingDefinition);
                    pendingDefinition = null;
                    fadeState = FadeState.FadingIn;
                    break;
                }

                case FadeState.FadingIn:
                {
                    float fadeInDuration = Mathf.Max(0.05f, ResolveFadeIn(activeDefinition));
                    float step = deltaTime / fadeInDuration;
                    currentVolume = Mathf.MoveTowards(currentVolume, 1f, step);
                    audioSource.volume = currentVolume * settings.MasterVolume * (activeDefinition?.Volume ?? 1f);

                    if (Mathf.Approximately(currentVolume, 1f))
                    {
                        fadeState = FadeState.Idle;
                    }

                    break;
                }
            }
        }

        private void ApplyDefinition(AmbientDefinition definition)
        {
            EnsureAudioSource();

            if (definition == null || string.IsNullOrEmpty(definition.Clip))
            {
                audioSource.Stop();
                activeDefinition = null;
                currentVolume = 0f;
                return;
            }

            try
            {
                var audioClip = Resources.Load<AudioClip>(definition.Clip);
                if (audioClip == null)
                {
                    ModLogger.Warning($"Clip de audio '{definition.Clip}' no encontrado.");
                    activeDefinition = null;
                    return;
                }

                audioSource.clip = audioClip;
                audioSource.loop = true;
                audioSource.volume = 0f;
                audioSource.priority = Mathf.RoundToInt(Mathf.Lerp(0, 256, Mathf.Clamp01(1f - settings.Priority)));
                audioSource.spatialBlend = definition.Radius > 0f ? 1f : 0f;
                audioSource.maxDistance = definition.Radius > 0f ? definition.Radius : 15f;
                audioSource.Play();

                activeDefinition = definition;
                currentVolume = 0f;
            }
            catch (Exception exception)
            {
                ModLogger.Error($"No se pudo reproducir clip de ambiente '{definition.Clip}': {exception}");
                activeDefinition = null;
            }
        }

        private AmbientDefinition SelectDefinition(World world, EntityPlayerLocal player)
        {
            var definitions = settings?.Sounds;
            if (definitions == null || definitions.Length == 0)
            {
                return null;
            }

            string biome = DetermineBiome(world, player);
            string condition = DetermineCondition(world, player);

            var candidates = definitions.Where(d => MatchesBiome(d, biome) && MatchesCondition(d, condition)).ToList();
            if (candidates.Count == 0)
            {
                candidates = definitions.Where(d => MatchesBiome(d, biome) && MatchesCondition(d, "default")).ToList();
            }

            return candidates.FirstOrDefault();
        }

        private static bool MatchesBiome(AmbientDefinition definition, string biome)
        {
            return string.Equals(definition.Biome, "any", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(definition.Biome, biome, StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesCondition(AmbientDefinition definition, string condition)
        {
            return string.Equals(definition.Condition, condition, StringComparison.OrdinalIgnoreCase);
        }

        private bool DefinitionsEqual(AmbientDefinition a, AmbientDefinition b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a == null || b == null)
            {
                return false;
            }

            return string.Equals(a.Biome, b.Biome, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(a.Condition, b.Condition, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(a.Clip, b.Clip, StringComparison.Ordinal) &&
                   Mathf.Approximately(a.Volume, b.Volume);
        }

        private void UpdateSpatialBlend(EntityPlayerLocal player)
        {
            if (audioSource == null || !audioSource.isPlaying)
            {
                return;
            }

            if (activeDefinition == null)
            {
                audioSource.spatialBlend = 0f;
                return;
            }

            if (activeDefinition.Radius <= 0f)
            {
                audioSource.transform.position = player.transform.position;
                audioSource.spatialBlend = 0f;
                return;
            }

            audioSource.transform.position = player.transform.position;
            audioSource.maxDistance = Mathf.Max(5f, activeDefinition.Radius);
            audioSource.spatialBlend = 1f;
        }

        private void EnsureAudioSource()
        {
            if (audioSource != null)
            {
                return;
            }

            audioRoot = new GameObject("ZombiesDeOP_AmbientAudio");
            DontDestroyOnLoad(audioRoot);
            audioSource = audioRoot.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = true;
        }

        private string DetermineBiome(World world, EntityPlayerLocal player)
        {
            if (world == null || player == null)
            {
                return "any";
            }

            try
            {
                var biomeMapProperty = world.GetType().GetProperty("BiomeMap", AccessTools.all);
                var biomeMap = biomeMapProperty?.GetValue(world);
                if (biomeMap == null)
                {
                    return "any";
                }

                Vector3 position = player.position;
                int x = Mathf.FloorToInt(position.x);
                int y = Mathf.FloorToInt(position.y);
                int z = Mathf.FloorToInt(position.z);

                var method = biomeMap.GetType().GetMethod("GetBiomeAt", new[] { typeof(int), typeof(int), typeof(int) }) ??
                             biomeMap.GetType().GetMethod("GetBiome", new[] { typeof(int), typeof(int), typeof(int) });

                object biome = null;
                if (method != null)
                {
                    biome = method.Invoke(biomeMap, new object[] { x, y, z });
                }

                if (biome == null)
                {
                    return "any";
                }

                var nameField = biome.GetType().GetField("m_sBiomeName", AccessTools.all);
                if (nameField != null)
                {
                    var value = nameField.GetValue(biome) as string;
                    if (!string.IsNullOrEmpty(value))
                    {
                        return value.ToLowerInvariant();
                    }
                }

                var nameProperty = biome.GetType().GetProperty("m_sBiomeName", AccessTools.all) ??
                                   biome.GetType().GetProperty("biomeName", AccessTools.all);
                if (nameProperty != null)
                {
                    var value = nameProperty.GetValue(biome) as string;
                    if (!string.IsNullOrEmpty(value))
                    {
                        return value.ToLowerInvariant();
                    }
                }
            }
            catch
            {
                // Ignorar fallos de detección de bioma.
            }

            return "any";
        }

        private string DetermineCondition(World world, EntityPlayerLocal player)
        {
            if (world == null)
            {
                return "default";
            }

            try
            {
                var weatherManager = GameManager.Instance?.WeatherManager;
                if (weatherManager != null)
                {
                    float precipitation = ReadFloatMember(weatherManager, new[] { "precipitationStrength", "PrecipitationStrength", "RainStrength" });
                    if (precipitation > 0.6f)
                    {
                        return "storm";
                    }

                    if (precipitation > 0.2f)
                    {
                        return "rain";
                    }

                    float wind = ReadFloatMember(weatherManager, new[] { "windStrength", "WindStrength" });
                    if (wind > 0.5f)
                    {
                        return "wind";
                    }
                }

                if (player != null && player.IsCrouching)
                {
                    return "cave";
                }
            }
            catch
            {
                // Ignorado.
            }

            return "default";
        }

        private static float ReadFloatMember(object instance, IEnumerable<string> names)
        {
            if (instance == null)
            {
                return 0f;
            }

            var type = instance.GetType();
            foreach (var name in names)
            {
                var property = AccessTools.Property(type, name);
                if (property != null && property.PropertyType == typeof(float))
                {
                    return (float)property.GetValue(instance, null);
                }

                var field = AccessTools.Field(type, name);
                if (field != null && field.FieldType == typeof(float))
                {
                    return (float)field.GetValue(instance);
                }
            }

            return 0f;
        }

        private float ResolveFadeIn(AmbientDefinition definition)
        {
            if (definition == null)
            {
                return settings.DefaultFadeIn;
            }

            return definition.FadeIn >= 0f ? definition.FadeIn : settings.DefaultFadeIn;
        }

        private float ResolveFadeOut(AmbientDefinition definition)
        {
            if (definition == null)
            {
                return settings.DefaultFadeOut;
            }

            return definition.FadeOut >= 0f ? definition.FadeOut : settings.DefaultFadeOut;
        }
    }
}
