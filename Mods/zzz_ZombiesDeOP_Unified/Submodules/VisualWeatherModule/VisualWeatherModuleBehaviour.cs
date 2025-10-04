using System;
using HarmonyLib;
using UnityEngine;
using ZombiesDeOPUnified.Core;

namespace ZombiesDeOPUnified.Submodules.VisualWeatherModule
{
    public sealed class VisualWeatherModuleBehaviour : MonoBehaviour, IModule
    {
        private const float DefaultDayLength = 24000f;

        private static readonly int RainOpacityShaderId = Shader.PropertyToID("_ClimaVisual_RainOpacity");
        private static readonly int CloudDensityShaderId = Shader.PropertyToID("_ClimaVisual_CloudDensity");

        private ModConfig.VisualWeatherSettings settings;
        private float smoothedFogDensity;
        private Color smoothedFogColor = Color.black;
        private Color smoothedAmbientColor = Color.black;

        public bool IsEnabled => VisualWeatherConfig.EnableVisualWeather;
        public string ModuleName => "Control Visual Climático";

        public void InitializeModule()
        {
            settings = VisualWeatherConfig.Settings;
            ModConfig.ConfigReloaded += OnConfigReloaded;
            enabled = IsEnabled;
        }

        public void Shutdown()
        {
            ModConfig.ConfigReloaded -= OnConfigReloaded;
            enabled = false;
        }

        private void OnConfigReloaded()
        {
            settings = VisualWeatherConfig.Settings;
            smoothedFogColor = Color.black;
            smoothedFogDensity = 0f;
            smoothedAmbientColor = Color.black;
        }

        private void Update()
        {
            if (!IsEnabled || settings == null || !settings.Enabled)
            {
                return;
            }

            var world = GameManager.Instance?.World;
            if (world == null)
            {
                return;
            }

            float normalizedTime = GetNormalizedTime(world);
            float daylightFactor = EvaluateDaylightFactor(normalizedTime);
            float deltaTime = Time.deltaTime <= 0f ? 0.016f : Time.deltaTime;

            if (settings.Fog.Enabled)
            {
                ApplyFogSettings(daylightFactor, deltaTime);
            }

            if (settings.Clouds.Enabled)
            {
                ApplyCloudSettings(daylightFactor);
            }

            if (settings.Rain.Enabled)
            {
                ApplyRainSettings(daylightFactor, deltaTime);
            }

            if (settings.Sun.Enabled)
            {
                ApplySunSettings(daylightFactor, deltaTime);
            }
        }

        private void ApplyFogSettings(float daylightFactor, float deltaTime)
        {
            float targetDensity = Mathf.Lerp(settings.Fog.NightIntensity, settings.Fog.DayIntensity, daylightFactor);
            Color targetColor = Color.Lerp(settings.Fog.NightColor, settings.Fog.DayColor, daylightFactor);

            smoothedFogDensity = Mathf.Lerp(smoothedFogDensity <= 0f ? targetDensity : smoothedFogDensity, targetDensity, deltaTime * settings.TransitionSpeed);
            smoothedFogColor = Color.Lerp(smoothedFogColor == default ? targetColor : smoothedFogColor, targetColor, deltaTime * settings.TransitionSpeed);

            RenderSettings.fog = true;
            RenderSettings.fogDensity = Mathf.Max(0f, smoothedFogDensity);
            RenderSettings.fogColor = smoothedFogColor;
        }

        private void ApplyCloudSettings(float daylightFactor)
        {
            float targetDensity = Mathf.Lerp(settings.Clouds.NightDensity, settings.Clouds.DayDensity, daylightFactor);

            Shader.SetGlobalFloat(CloudDensityShaderId, Mathf.Clamp01(targetDensity));

            var weatherManager = GameManager.Instance?.WeatherManager;
            if (weatherManager != null)
            {
                SetFieldIfExists(weatherManager, "cloudOpacity", targetDensity);
                SetPropertyIfExists(weatherManager, "CloudOpacity", targetDensity);
            }
        }

        private void ApplyRainSettings(float daylightFactor, float deltaTime)
        {
            float targetIntensity = Mathf.Lerp(settings.Rain.NightIntensity, settings.Rain.DayIntensity, daylightFactor);
            Shader.SetGlobalFloat(RainOpacityShaderId, Mathf.Clamp01(settings.Rain.Opacity));

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
                SetPropertyIfExists(precipitationController, "Opacity", settings.Rain.Opacity);
                SetFieldIfExists(precipitationController, "opacity", settings.Rain.Opacity);
            }
        }

        private void ApplySunSettings(float daylightFactor, float deltaTime)
        {
            Color dawnToDay = Color.Lerp(settings.Sun.DawnColor, settings.Sun.DayColor, Mathf.SmoothStep(0f, 0.5f, daylightFactor));
            Color duskToNight = Color.Lerp(settings.Sun.DuskColor, settings.Sun.NightColor, Mathf.SmoothStep(0f, 0.5f, 1f - daylightFactor));
            Color targetAmbient = Color.Lerp(dawnToDay, duskToNight, 0.5f);

            smoothedAmbientColor = Color.Lerp(smoothedAmbientColor == default ? targetAmbient : smoothedAmbientColor, targetAmbient, deltaTime * settings.TransitionSpeed);
            RenderSettings.ambientLight = smoothedAmbientColor;

            var skyManager = GameManager.Instance?.SkyManager;
            if (skyManager == null)
            {
                return;
            }

            SetFieldIfExists(skyManager, "dawnColor", settings.Sun.DawnColor);
            SetFieldIfExists(skyManager, "duskColor", settings.Sun.DuskColor);
            SetFieldIfExists(skyManager, "dayColor", settings.Sun.DayColor);
            SetFieldIfExists(skyManager, "nightColor", settings.Sun.NightColor);
            SetPropertyIfExists(skyManager, "SunColor", Color.Lerp(settings.Sun.DawnColor, settings.Sun.DuskColor, daylightFactor));
        }

        private float GetNormalizedTime(World world)
        {
            if (world == null)
            {
                return 0f;
            }

            float worldTime = Convert.ToSingle(world.worldTime);
            float ticksPerDay = DefaultDayLength;

            var dayLengthField = world.GetType().GetField("ticksPerDay", AccessTools.all);
            if (dayLengthField != null && dayLengthField.FieldType == typeof(int))
            {
                ticksPerDay = Convert.ToSingle((int)dayLengthField.GetValue(world));
            }

            return Mathf.Repeat(worldTime, ticksPerDay) / Mathf.Max(1f, ticksPerDay);
        }

        private float EvaluateDaylightFactor(float normalizedTime)
        {
            if (normalizedTime < settings.DawnStart)
            {
                return 0f;
            }

            if (normalizedTime < settings.DayStart)
            {
                return Mathf.InverseLerp(settings.DawnStart, settings.DayStart, normalizedTime);
            }

            if (normalizedTime < settings.DuskStart)
            {
                return 1f;
            }

            if (normalizedTime < settings.NightStart)
            {
                return 1f - Mathf.InverseLerp(settings.DuskStart, settings.NightStart, normalizedTime);
            }

            return 0f;
        }

        private static void SetFieldIfExists(object target, string fieldName, object value)
        {
            if (target == null)
            {
                return;
            }

            var field = AccessTools.Field(target.GetType(), fieldName);
            if (field != null)
            {
                try
                {
                    field.SetValue(target, value);
                }
                catch
                {
                    // Ignorado.
                }
            }
        }

        private static void SetPropertyIfExists(object target, string propertyName, object value)
        {
            if (target == null)
            {
                return;
            }

            var property = AccessTools.Property(target.GetType(), propertyName);
            if (property != null && property.CanWrite)
            {
                try
                {
                    property.SetValue(target, value, null);
                }
                catch
                {
                    // Ignorado.
                }
            }
        }

        private static T GetFieldValue<T>(object target, string fieldName) where T : class
        {
            if (target == null)
            {
                return null;
            }

            var field = AccessTools.Field(target.GetType(), fieldName);
            if (field == null)
            {
                return null;
            }

            try
            {
                return field.GetValue(target) as T;
            }
            catch
            {
                return null;
            }
        }
    }
}
