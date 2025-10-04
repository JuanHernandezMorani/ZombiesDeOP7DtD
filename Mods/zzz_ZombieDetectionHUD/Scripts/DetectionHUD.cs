using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using HarmonyLib;
using UnityEngine;

namespace ZombieDetectionHUD
{
    /// <summary>
    /// Harmony patcher entry point defined in ModInfo.xml.
    /// Ensures the HUD behaviour is attached to the local player and cleans up gracefully.
    /// </summary>
    [HarmonyPatch]
    public static class DetectionHudPatcher
    {
        static DetectionHudPatcher()
        {
            DetectionHudLogger.Info("Inicializando ZombieDetectionHUD...");
            DetectionHudCompatibility.DetectWar3zuk();
            DetectionHudConfig.EnsureLoaded();
        }

        [HarmonyPatch(typeof(EntityPlayerLocal), "Init"), HarmonyPostfix]
        public static void AttachHud(EntityPlayerLocal __instance)
        {
            if (__instance == null)
            {
                return;
            }

            DetectionHudBehaviour.Ensure(__instance);
        }

        [HarmonyPatch(typeof(GameManager), "OnApplicationQuit"), HarmonyPostfix]
        public static void OnQuit()
        {
            DetectionHudBehaviour.Cleanup();
        }
    }

    /// <summary>
    /// Manages configuration loading from the Config/DetectionHUDConfig.xml file.
    /// Provides default values when the file is missing or malformed.
    /// </summary>
    public sealed class DetectionHudConfig
    {
        private static readonly object SyncRoot = new();
        private static DetectionHudConfig instance;

        public static DetectionHudConfig Instance
        {
            get
            {
                EnsureLoaded();
                return instance;
            }
        }

        public float UpdateInterval { get; private set; } = 0.5f;
        public float DetectionRadius { get; private set; } = 50f;
        public float StealthRadius { get; private set; } = 35f;
        public float LineOfSightMaxAngle { get; private set; } = 85f;
        public HudAnchor Anchor { get; private set; } = HudAnchor.TopRight;
        public Vector2 Offset { get; private set; } = new(32f, 64f);
        public string DetectedText { get; private set; } = "[DETECTADO]";
        public string HiddenText { get; private set; } = "[OCULTO]";
        public int FontSize { get; private set; } = 32;
        public bool UseOutline { get; private set; } = true;
        public bool DebugLogging { get; private set; }
        public static void EnsureLoaded()
        {
            if (instance != null)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (instance == null)
                {
                    instance = Load();
                }
            }
        }

        private static DetectionHudConfig Load()
        {
            try
            {
                var configPath = Path.Combine(DetectionHudPaths.ModRoot, "Config", "DetectionHUDConfig.xml");
                if (!File.Exists(configPath))
                {
                    DetectionHudLogger.Warn($"No se encontró el archivo de configuración en {configPath}. Se usarán valores por defecto.");
                    return new DetectionHudConfig();
                }

                var document = XDocument.Load(configPath);
                var config = new DetectionHudConfig();
                var root = document.Root;
                if (root == null)
                {
                    DetectionHudLogger.Warn("El archivo de configuración no tiene nodo raíz. Se usarán valores por defecto.");
                    return config;
                }

                config.UpdateInterval = ReadFloat(root, "UpdateInterval", config.UpdateInterval, 0.1f, 5f);
                config.DetectionRadius = ReadFloat(root, "DetectionRadius", config.DetectionRadius, 1f, 200f);
                config.StealthRadius = ReadFloat(root, "StealthRadius", config.StealthRadius, 1f, 200f);
                config.LineOfSightMaxAngle = ReadFloat(root, "LineOfSightMaxAngle", config.LineOfSightMaxAngle, 0f, 180f);

                var positionElement = root.Element("Position");
                if (positionElement != null)
                {
                    var anchorValue = ReadString(positionElement, "Anchor", config.Anchor.ToString());
                    if (!Enum.TryParse(anchorValue, true, out HudAnchor anchor))
                    {
                        anchor = config.Anchor;
                        DetectionHudLogger.Warn($"Valor de anclaje desconocido '{anchorValue}'. Se mantiene {anchor}.");
                    }

                    config.Anchor = anchor;
                    var offsetX = ReadFloat(positionElement, "OffsetX", config.Offset.x, -4096f, 4096f);
                    var offsetY = ReadFloat(positionElement, "OffsetY", config.Offset.y, -4096f, 4096f);
                    config.Offset = new Vector2(offsetX, offsetY);
                }

                var textElement = root.Element("Text");
                if (textElement != null)
                {
                    config.DetectedText = ReadString(textElement, "Detected", config.DetectedText);
                    config.HiddenText = ReadString(textElement, "Hidden", config.HiddenText);
                    config.FontSize = Mathf.Clamp(Mathf.RoundToInt(ReadFloat(textElement, "FontSize", config.FontSize, 8f, 96f)), 8, 96);
                    config.UseOutline = ReadBool(textElement, "Outline", config.UseOutline);
                }

                var debugElement = root.Element("Debug");
                if (debugElement != null)
                {
                    config.DebugLogging = ReadBool(debugElement, "Enabled", config.DebugLogging);
                }

                DetectionHudLogger.Info("Configuración de ZombieDetectionHUD cargada correctamente.");
                return config;
            }
            catch (Exception ex)
            {
                DetectionHudLogger.Error("Error al cargar la configuración del HUD. Se usarán valores por defecto.", ex);
                return new DetectionHudConfig();
            }
        }

        private static float ReadFloat(XElement parent, string name, float defaultValue, float min, float max)
        {
            var element = parent.Element(name);
            if (element == null)
            {
                return defaultValue;
            }

            var valueAttribute = element.Attribute("value") ?? element.Attribute("Value");
            if (valueAttribute == null)
            {
                return defaultValue;
            }

            if (float.TryParse(valueAttribute.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                return Mathf.Clamp(parsed, min, max);
            }

            return defaultValue;
        }

        private static string ReadString(XElement parent, string name, string defaultValue)
        {
            var element = parent.Element(name);
            if (element == null)
            {
                return defaultValue;
            }

            var valueAttribute = element.Attribute("value") ?? element.Attribute("Value");
            return valueAttribute?.Value ?? defaultValue;
        }

        private static bool ReadBool(XElement parent, string name, bool defaultValue)
        {
            var element = parent.Element(name);
            if (element == null)
            {
                return defaultValue;
            }

            var valueAttribute = element.Attribute("value") ?? element.Attribute("Value");
            if (valueAttribute == null)
            {
                return defaultValue;
            }

            if (bool.TryParse(valueAttribute.Value, out var parsed))
            {
                return parsed;
            }

            return defaultValue;
        }
    }

    /// <summary>
    /// Determines runtime compatibility flags and logs detection of other mods.
    /// </summary>
    public static class DetectionHudCompatibility
    {
        private static bool initialized;

        public static bool IsWar3zukLoaded { get; private set; }

        public static void DetectWar3zuk()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            try
            {
                var war3zukType = AccessTools.TypeByName("War3zukAIO.War3zukMain") ?? AccessTools.TypeByName("War3zukAIO.War3zukManager");
                IsWar3zukLoaded = war3zukType != null;
                if (IsWar3zukLoaded)
                {
                    DetectionHudLogger.Info("War3zuk AIO detectado. Se aplicará modo de compatibilidad.");
                }
                else
                {
                    DetectionHudLogger.Info("War3zuk AIO no encontrado. Funcionando en modo estándar.");
                }
            }
            catch (Exception ex)
            {
                DetectionHudLogger.Warn("No se pudo comprobar la presencia de War3zuk AIO.");
                DetectionHudLogger.Debug(ex.ToString());
            }
        }
    }

    /// <summary>
    /// Provides strongly typed logging helpers with consistent prefixes.
    /// </summary>
    public static class DetectionHudLogger
    {
        private const string Prefix = "[ZombieDetectionHUD]";

        public static void Info(string message)
        {
            Log.Out($"{Prefix} {message}");
        }

        public static void Warn(string message)
        {
            Log.Warning($"{Prefix} {message}");
        }

        public static void Error(string message, Exception exception)
        {
            Log.Error($"{Prefix} {message}\n{exception}");
        }

        public static void Debug(string message)
        {
            if (DetectionHudConfig.Instance.DebugLogging)
            {
                Log.Out($"{Prefix} DEBUG: {message}");
            }
        }
    }

    /// <summary>
    /// Calculates file system paths relative to the compiled assembly.
    /// </summary>
    public static class DetectionHudPaths
    {
        private static string modRoot;

        public static string ModRoot
        {
            get
            {
                if (!string.IsNullOrEmpty(modRoot))
                {
                    return modRoot;
                }

                try
                {
                    var location = Assembly.GetExecutingAssembly().Location;
                    if (string.IsNullOrEmpty(location))
                    {
                        return modRoot = Path.Combine(Directory.GetCurrentDirectory(), "Mods", "zzz_ZombieDetectionHUD");
                    }

                    var directory = Path.GetDirectoryName(location);
                    if (string.IsNullOrEmpty(directory))
                    {
                        return modRoot = Path.Combine(Directory.GetCurrentDirectory(), "Mods", "zzz_ZombieDetectionHUD");
                    }

                    var parent = Directory.GetParent(directory);
                    modRoot = parent?.FullName ?? directory;
                }
                catch (Exception ex)
                {
                    DetectionHudLogger.Error("No se pudo determinar la ruta del mod.", ex);
                    modRoot = Path.Combine(Directory.GetCurrentDirectory(), "Mods", "zzz_ZombieDetectionHUD");
                }

                return modRoot;
            }
        }
    }

    /// <summary>
    /// Handles runtime HUD updates and detection polling for the local player.
    /// </summary>
    public sealed class DetectionHudBehaviour : MonoBehaviour
    {
        private static readonly Dictionary<int, DetectionHudBehaviour> ActiveByEntityId = new();

        private EntityPlayerLocal player;
        private Texture2D detectedIcon;
        private Texture2D hiddenIcon;
        private GUIStyle detectionStyle;
        private Coroutine detectionRoutine;
        private bool isDetected;
        private float updateInterval;
        private float detectionRadius;
        private float stealthRadius;
        private float lineOfSightMaxAngle;
        private string detectedText;
        private string hiddenText;
        private bool useOutline;

        public static void Ensure(EntityPlayerLocal player)
        {
            if (player == null)
            {
                return;
            }

            var entityId = player.entityId;
            if (ActiveByEntityId.TryGetValue(entityId, out var existing) && existing != null)
            {
                existing.Bind(player);
                return;
            }

            var go = player.gameObject;
            if (go == null)
            {
                DetectionHudLogger.Warn("El EntityPlayerLocal no tiene GameObject asociado para adjuntar el HUD.");
                return;
            }

            var behaviour = go.GetComponent<DetectionHudBehaviour>() ?? go.AddComponent<DetectionHudBehaviour>();
            behaviour.Bind(player);
            ActiveByEntityId[entityId] = behaviour;
        }

        public static void Cleanup()
        {
            foreach (var behaviour in ActiveByEntityId.Values.ToList())
            {
                if (behaviour != null)
                {
                    behaviour.StopAllCoroutines();
                    UnityEngine.Object.Destroy(behaviour);
                }
            }

            ActiveByEntityId.Clear();
        }

        private void Bind(EntityPlayerLocal targetPlayer)
        {
            player = targetPlayer;
            ApplyConfig(DetectionHudConfig.Instance);
            if (DetectionHudCompatibility.IsWar3zukLoaded)
            {
                DetectionHudLogger.Debug("Compatibilidad War3zuk activa para el jugador local.");
            }
            LoadAssets();

            if (detectionRoutine != null)
            {
                StopCoroutine(detectionRoutine);
            }

            detectionRoutine = StartCoroutine(DetectionLoop());
        }

        private void OnDisable()
        {
            if (detectionRoutine != null)
            {
                StopCoroutine(detectionRoutine);
                detectionRoutine = null;
            }
        }

        private void ApplyConfig(DetectionHudConfig config)
        {
            updateInterval = Mathf.Max(0.1f, config.UpdateInterval);
            detectionRadius = Mathf.Max(1f, config.DetectionRadius);
            stealthRadius = Mathf.Clamp(config.StealthRadius, 1f, detectionRadius);
            lineOfSightMaxAngle = Mathf.Clamp(config.LineOfSightMaxAngle, 0f, 180f);
            detectedText = config.DetectedText;
            hiddenText = config.HiddenText;
            useOutline = config.UseOutline;
        }

        private void LoadAssets()
        {
            if (detectedIcon == null)
            {
                detectedIcon = LoadTexture("UI/Textures/detected.png");
            }

            if (hiddenIcon == null)
            {
                hiddenIcon = LoadTexture("UI/Textures/hidden.png");
            }
        }

        private static Texture2D LoadTexture(string relativePath)
        {
            try
            {
                var fullPath = Path.Combine(DetectionHudPaths.ModRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(fullPath))
                {
                    DetectionHudLogger.Warn($"No se encontró la textura requerida: {relativePath}");
                    return Texture2D.whiteTexture;
                }

                var data = File.ReadAllBytes(fullPath);
                var texture = new Texture2D(2, 2, TextureFormat.ARGB32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };

                if (!texture.LoadImage(data))
                {
                    DetectionHudLogger.Warn($"No se pudo cargar la textura {relativePath}. Se usará un color sólido.");
                    return Texture2D.whiteTexture;
                }

                return texture;
            }
            catch (Exception ex)
            {
                DetectionHudLogger.Error($"Error al cargar la textura {relativePath}", ex);
                return Texture2D.whiteTexture;
            }
        }

        private IEnumerator DetectionLoop()
        {
            var wait = new WaitForSeconds(updateInterval);
            while (true)
            {
                yield return wait;
                UpdateDetectionState();
            }
        }

        private void UpdateDetectionState()
        {
            if (player == null || GameManager.Instance == null)
            {
                SetDetected(false);
                return;
            }

            if (GameManager.Instance.IsPaused())
            {
                return;
            }

            if (player.IsDead())
            {
                SetDetected(false);
                return;
            }

            var world = player.world ?? GameManager.Instance.World;
            if (world == null)
            {
                SetDetected(false);
                return;
            }

            var playerPosition = player.position;
            var radius = PlayerStateHelper.IsPlayerStealthed(player) ? stealthRadius : detectionRadius;
            var detected = DetectionEvaluator.IsPlayerDetected(world, player, playerPosition, radius, lineOfSightMaxAngle);
            SetDetected(detected);
        }

        private void SetDetected(bool detected)
        {
            if (isDetected == detected)
            {
                return;
            }

            isDetected = detected;
            DetectionHudLogger.Debug($"Estado actualizado: {(isDetected ? "DETECTADO" : "OCULTO")}");
        }

        private void EnsureStyle()
        {
            if (detectionStyle != null)
            {
                return;
            }

            detectionStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize = Mathf.RoundToInt(DetectionHudConfig.Instance.FontSize * (Screen.height / 1080f)),
                fontStyle = FontStyle.Bold,
                richText = false
            };
        }

        private void OnGUI()
        {
            if (Event.current == null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            if (player == null || GameManager.Instance == null || GameManager.Instance.IsPaused())
            {
                return;
            }

            if (Time.timeScale <= 0f)
            {
                return;
            }

            EnsureStyle();

            var settings = DetectionHudConfig.Instance;
            var text = isDetected ? detectedText : hiddenText;
            var color = isDetected ? new Color32(220, 40, 40, 255) : new Color32(40, 180, 90, 255);
            var icon = isDetected ? detectedIcon : hiddenIcon;
            float scale = Mathf.Max(0.75f, Screen.height / 1080f);
            detectionStyle.fontSize = Mathf.RoundToInt(settings.FontSize * scale);
            float spacing = 12f * scale;
            float iconSize = Mathf.Max(48f * scale, detectionStyle.fontSize * 1.5f);
            Vector2 labelSize = detectionStyle.CalcSize(new GUIContent(text));
            float totalWidth = labelSize.x + spacing + iconSize;
            float totalHeight = Mathf.Max(iconSize, labelSize.y);
            Vector2 offset = settings.Offset * scale;

            float startX;
            float startY;
            switch (settings.Anchor)
            {
                case HudAnchor.TopLeft:
                    startX = offset.x;
                    startY = offset.y;
                    break;
                case HudAnchor.BottomLeft:
                    startX = offset.x;
                    startY = Screen.height - offset.y - totalHeight;
                    break;
                case HudAnchor.BottomRight:
                    startX = Screen.width - offset.x - totalWidth;
                    startY = Screen.height - offset.y - totalHeight;
                    break;
                case HudAnchor.Center:
                    startX = Screen.width / 2f - totalWidth / 2f + offset.x;
                    startY = Screen.height / 2f - totalHeight / 2f + offset.y;
                    break;
                case HudAnchor.TopRight:
                default:
                    startX = Screen.width - offset.x - totalWidth;
                    startY = offset.y;
                    break;
            }

            var labelRect = new Rect(startX, startY + (totalHeight - labelSize.y) / 2f, labelSize.x, labelSize.y);
            var iconRect = new Rect(startX + labelSize.x + spacing, startY + (totalHeight - iconSize) / 2f, iconSize, iconSize);

            var previousColor = GUI.color;
            GUI.color = color;
            if (icon != null)
            {
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);
            }

            DrawLabelWithOptionalOutline(labelRect, text, color);
            GUI.color = previousColor;
        }

        private void DrawLabelWithOptionalOutline(Rect rect, string text, Color color)
        {
            if (!useOutline)
            {
                var originalColor = detectionStyle.normal.textColor;
                detectionStyle.normal.textColor = color;
                GUI.Label(rect, text, detectionStyle);
                detectionStyle.normal.textColor = originalColor;
                return;
            }

            var backupColor = detectionStyle.normal.textColor;
            detectionStyle.normal.textColor = Color.black;
            const float outlineOffset = 1.5f;
            GUI.Label(new Rect(rect.x - outlineOffset, rect.y, rect.width, rect.height), text, detectionStyle);
            GUI.Label(new Rect(rect.x + outlineOffset, rect.y, rect.width, rect.height), text, detectionStyle);
            GUI.Label(new Rect(rect.x, rect.y - outlineOffset, rect.width, rect.height), text, detectionStyle);
            GUI.Label(new Rect(rect.x, rect.y + outlineOffset, rect.width, rect.height), text, detectionStyle);

            detectionStyle.normal.textColor = color;
            GUI.Label(rect, text, detectionStyle);
            detectionStyle.normal.textColor = backupColor;
        }
    }

    public enum HudAnchor
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        Center
    }

    /// <summary>
    /// Evaluates detection state by scanning nearby entities and validating vision constraints.
    /// </summary>
    public static class DetectionEvaluator
    {
        private static bool hasLoggedReflectionError;

        public static bool IsPlayerDetected(World world, EntityPlayerLocal player, Vector3 playerPosition, float radius, float maxAngle)
        {
            if (world == null || player == null)
            {
                return false;
            }

            float radiusSquared = radius * radius;
            foreach (var enemy in DetectionHudEnemyScanner.GetNearbyEnemies(world, playerPosition, radius))
            {
                if (enemy == null || enemy.IsDead())
                {
                    continue;
                }

                var enemyPosition = enemy.position;
                var distanceSquared = (enemyPosition - playerPosition).sqrMagnitude;
                if (distanceSquared > radiusSquared)
                {
                    continue;
                }

                if (!HasLineOfSight(enemy, player, playerPosition, enemyPosition, maxAngle))
                {
                    continue;
                }

                DetectionHudLogger.Debug($"Detectado por {enemy.EntityName} (ID {enemy.entityId}).");
                return true;
            }

            return false;
        }

        private static bool HasLineOfSight(EntityEnemy enemy, EntityPlayerLocal player, Vector3 playerPosition, Vector3 enemyPosition, float maxAngle)
        {
            try
            {
                var senses = enemy.Senses;
                if (senses != null)
                {
                    var targetField = AccessTools.Field(senses.GetType(), "target");
                    if (targetField?.GetValue(senses) is Entity entity && entity.entityId == player.entityId)
                    {
                        return true;
                    }

                    var canSeeMethod = senses.GetType().GetMethod("CanSee", new[] { typeof(Entity) });
                    if (canSeeMethod != null)
                    {
                        if (canSeeMethod.Invoke(senses, new object[] { player }) is bool visible && visible)
                        {
                            return true;
                        }
                    }
                }

                var attackTargetField = AccessTools.Field(enemy.GetType(), "attackTarget");
                if (attackTargetField?.GetValue(enemy) is Entity target && target.entityId == player.entityId)
                {
                    return true;
                }

                var getAttackTarget = enemy.GetType().GetMethod("GetAttackTarget", Type.EmptyTypes);
                if (getAttackTarget != null)
                {
                    if (getAttackTarget.Invoke(enemy, Array.Empty<object>()) is Entity targetEntity && targetEntity.entityId == player.entityId)
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                if (!hasLoggedReflectionError)
                {
                    hasLoggedReflectionError = true;
                    DetectionHudLogger.Error("Error comprobando línea de visión mediante reflexión.", ex);
                }
            }

            Vector3 toPlayer = (playerPosition - enemyPosition).normalized;
            Vector3 forward = enemy.forward;
            float angle = Vector3.Angle(forward, toPlayer);
            if (angle <= maxAngle)
            {
                if (Physics.Linecast(enemyPosition + Vector3.up, playerPosition + Vector3.up, out var hitInfo))
                {
                    return hitInfo.collider == null || hitInfo.collider.gameObject == player.gameObject;
                }

                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Utility responsible for finding nearby zombie enemies efficiently.
    /// </summary>
    public static class DetectionHudEnemyScanner
    {
        private static MethodInfo getEntitiesInBounds;
        private static bool attemptedLookup;
        private static bool fallbackLogged;

        public static IEnumerable<EntityEnemy> GetNearbyEnemies(World world, Vector3 center, float radius)
        {
            if (world == null)
            {
                yield break;
            }

            if (!attemptedLookup)
            {
                attemptedLookup = true;
                getEntitiesInBounds = world.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(m => m.Name == "GetEntitiesInBounds" &&
                                         m.GetParameters().Length == 3 &&
                                         m.GetParameters()[0].ParameterType == typeof(Type));
            }

            if (getEntitiesInBounds != null)
            {
                var list = new List<Entity>();
                try
                {
                    var bounds = new Bounds(center, new Vector3(radius * 2f, radius * 2f, radius * 2f));
                    getEntitiesInBounds.Invoke(world, new object[] { typeof(EntityEnemy), bounds, list });
                    foreach (var entity in list)
                    {
                        if (entity is EntityEnemy enemy)
                        {
                            yield return enemy;
                        }
                    }

                    yield break;
                }
                catch (Exception ex)
                {
                    getEntitiesInBounds = null;
                    if (!fallbackLogged)
                    {
                        fallbackLogged = true;
                        DetectionHudLogger.Error("Fallo al llamar a World.GetEntitiesInBounds. Se usará enumeración alternativa.", ex);
                    }
                }
            }

            foreach (var enemy in EnumerateAllEnemies(world))
            {
                if (enemy == null)
                {
                    continue;
                }

                if ((enemy.position - center).sqrMagnitude <= radius * radius)
                {
                    yield return enemy;
                }
            }
        }

        private static IEnumerable<EntityEnemy> EnumerateAllEnemies(World world)
        {
            object entityCollection = null;
            try
            {
                var entitiesProperty = world.GetType().GetProperty("Entities", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                entityCollection = entitiesProperty?.GetValue(world);
            }
            catch (Exception ex)
            {
                if (!fallbackLogged)
                {
                    fallbackLogged = true;
                    DetectionHudLogger.Error("No se pudo acceder a la colección de entidades del mundo.", ex);
                }
            }

            if (entityCollection == null)
            {
                yield break;
            }

            if (entityCollection is IEnumerable enumerable)
            {
                foreach (var entry in enumerable)
                {
                    if (entry is EntityEnemy enemy)
                    {
                        yield return enemy;
                    }
                }

                yield break;
            }

            var listField = entityCollection.GetType().GetField("list", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (listField?.GetValue(entityCollection) is IEnumerable listEnumerable)
            {
                foreach (var entry in listEnumerable)
                {
                    if (entry is EntityEnemy enemy)
                    {
                        yield return enemy;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Helper methods for determining the local player's stealth posture using reflection for compatibility.
    /// </summary>
    public static class PlayerStateHelper
    {
        public static bool IsPlayerStealthed(EntityPlayerLocal player)
        {
            if (player == null)
            {
                return false;
            }

            try
            {
                var property = AccessTools.Property(player.GetType(), "IsCrouching") ?? AccessTools.Property(player.GetType(), "IsSneaking") ?? AccessTools.Property(player.GetType(), "IsStealthed");
                if (property != null && property.PropertyType == typeof(bool))
                {
                    return (bool)property.GetValue(player, null);
                }

                var field = AccessTools.Field(player.GetType(), "isCrouching") ?? AccessTools.Field(player.GetType(), "isSneaking");
                if (field != null && field.FieldType == typeof(bool))
                {
                    return (bool)field.GetValue(player);
                }

                var stealthProperty = AccessTools.Property(player.GetType(), "StealthState");
                if (stealthProperty?.GetValue(player, null) is Enum stealthEnum)
                {
                    return Convert.ToInt32(stealthEnum) != 0;
                }
            }
            catch (Exception ex)
            {
                DetectionHudLogger.Debug($"No se pudo determinar el estado de sigilo: {ex.Message}");
            }

            return false;
        }
    }
}
