using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using ZombiesDeOP.Utilities;

namespace ZombiesDeOP.Systems
{
    public sealed class DetectionSystemRuntime : MonoBehaviour
    {
        private const float DEFAULT_INTERVAL = 0.3f;
        private const float DEFAULT_RADIUS = 30f;
        private const float WORLD_LOG_COOLDOWN = 5f;

        private static readonly MethodInfo EntitiesInBoundsWithBuffer = ResolveEntitiesInBounds(new[]
        {
            typeof(Type),
            typeof(Bounds),
            typeof(List<Entity>)
        });

        private static readonly MethodInfo EntitiesInBoundsReturningList = ResolveEntitiesInBounds(new[]
        {
            typeof(Type),
            typeof(Bounds)
        });

        private static readonly Type EntityFilterType = typeof(EntityAlive);

        private readonly List<Entity> _entityBuffer = new();
        private readonly List<EntityEnemy> _enemyBuffer = new();

        private float _lastTickTime;
        private float _lastWorldWarning;
        private UIOverlayComponent _overlay;
        private float _updateInterval;
        private float _radius;

        private static bool _warnedMissingReflection;
        private static bool _warnedMissingRuntime;
        private static bool _loggedCollectionMethod;

        public static DetectionSystemRuntime Instance { get; private set; }

        public static float DefaultUpdateInterval => DEFAULT_INTERVAL;
        public static float DefaultDetectionRadius => DEFAULT_RADIUS;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                ModLogger.Warn("⚠️ [ZombiesDeOP] Se detectó una instancia duplicada de DetectionSystemRuntime; destruyendo duplicado.");
                Destroy(this);
                return;
            }

            Instance = this;

            float configuredInterval = ModSettings.PollingInterval;
            _updateInterval = configuredInterval > 0f
                ? Mathf.Clamp(configuredInterval, 0.1f, 1f)
                : DEFAULT_INTERVAL;
            float configuredRadius = ModSettings.DetectionRange;
            _radius = configuredRadius > 0f ? Mathf.Clamp(configuredRadius, 5f, 60f) : DEFAULT_RADIUS;

            ModLogger.Info($"[ZombiesDeOP] DetectionSystemRuntime iniciado (interval={_updateInterval:F2}s, radius={_radius:F1}m)");
        }

        private void OnEnable()
        {
            EnsureOverlay();
        }

        private void Update()
        {
            if (Time.time - _lastTickTime < _updateInterval)
            {
                return;
            }

            _lastTickTime = Time.time;

            try
            {
                DoTick();
            }
            catch (Exception ex)
            {
                ModLogger.Error("❌ [ZombiesDeOP] Error en DetectionSystemRuntime.DoTick", ex);
            }
        }

        private void DoTick()
        {
            var world = GameManager.Instance?.World;
            var player = world?.GetPrimaryPlayer() as EntityPlayerLocal;

            if (world == null || player == null)
            {
                if (Time.time - _lastWorldWarning >= WORLD_LOG_COOLDOWN)
                {
                    _lastWorldWarning = Time.time;
                    ModLogger.LogDebug("[ZombiesDeOP] DetectionSystemRuntime sin mundo o jugador local disponible");
                }

                DetectionSystem.ResetState(_overlay);
                return;
            }

            var overlay = EnsureOverlay();

            CollectEnemies(world, player);

            float nearest = float.MaxValue;
            foreach (var enemy in _enemyBuffer)
            {
                float distance = enemy.GetDistance(player);
                if (distance < nearest)
                {
                    nearest = distance;
                }
            }

            if (ModSettings.DebugMode)
            {
                string distanceInfo = nearest < float.MaxValue ? nearest.ToString("F1") : "--";
                ModLogger.LogDebug($"[ZombiesDeOP] Tick detección -> enemigos: {_enemyBuffer.Count}, distancia mínima: {distanceInfo}m");
            }

            DetectionSystem.ProcessRuntimeTick(player, _enemyBuffer, _radius, overlay);
        }

        private void CollectEnemies(World world, EntityPlayerLocal player)
        {
            _entityBuffer.Clear();
            _enemyBuffer.Clear();

            Bounds bounds = new Bounds(player.position, Vector3.one * (_radius * 2f));

            if (!TryPopulateEntities(world, bounds))
            {
                if (!_warnedMissingReflection)
                {
                    _warnedMissingReflection = true;
                    ModLogger.Warn("⚠️ [ZombiesDeOP] No se pudo resolver World.GetEntitiesInBounds; usando lista vacía");
                }

                return;
            }

            foreach (var entity in _entityBuffer)
            {
                if (entity is EntityEnemy enemy && enemy.IsAlive())
                {
                    _enemyBuffer.Add(enemy);
                }
            }
        }

        private bool TryPopulateEntities(World world, Bounds bounds)
        {
            if (world == null)
            {
                return false;
            }

            try
            {
                if (EntitiesInBoundsWithBuffer != null)
                {
                    try
                    {
                        object[] args = { EntityFilterType, bounds, _entityBuffer };
                        EntitiesInBoundsWithBuffer.Invoke(world, args);
                        RemoveNullEntities();
                        LogCollectionMethod($"{EntitiesInBoundsWithBuffer.DeclaringType?.Name}.{EntitiesInBoundsWithBuffer.Name}(Type, Bounds, List<Entity>)");
                        return true;
                    }
                    catch (TargetInvocationException ex)
                    {
                        ModLogger.Error("❌ [ZombiesDeOP] Reflection invocation error (buffer overload)", ex.InnerException ?? ex);
                    }
                }

                if (EntitiesInBoundsReturningList != null)
                {
                    try
                    {
                        object[] args = { EntityFilterType, bounds };
                        object result = EntitiesInBoundsReturningList.Invoke(world, args);
                        if (result is IEnumerable enumerable)
                        {
                            foreach (var obj in enumerable)
                            {
                                if (obj is Entity entity && entity != null)
                                {
                                    _entityBuffer.Add(entity);
                                }
                            }

                            RemoveNullEntities();
                            LogCollectionMethod($"{EntitiesInBoundsReturningList.DeclaringType?.Name}.{EntitiesInBoundsReturningList.Name}(Type, Bounds)");
                            return true;
                        }
                    }
                    catch (TargetInvocationException ex)
                    {
                        ModLogger.Error("❌ [ZombiesDeOP] Reflection invocation error (list overload)", ex.InnerException ?? ex);
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("❌ [ZombiesDeOP] Error obteniendo entidades cercanas", ex);
            }

            return false;
        }

        private static MethodInfo ResolveEntitiesInBounds(Type[] signature)
        {
            MethodInfo direct = AccessTools.Method(typeof(World), "GetEntitiesInBounds", signature);
            if (direct != null)
            {
                return direct;
            }

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var method in typeof(World).GetMethods(flags))
            {
                if (!string.Equals(method.Name, "GetEntitiesInBounds", StringComparison.Ordinal))
                {
                    continue;
                }

                if (ParametersCompatible(method.GetParameters(), signature))
                {
                    return method;
                }
            }

            return null;
        }

        private static bool ParametersCompatible(IReadOnlyList<ParameterInfo> parameters, IReadOnlyList<Type> desired)
        {
            if (parameters.Count != desired.Count)
            {
                return false;
            }

            for (int i = 0; i < parameters.Count; i++)
            {
                var actualType = parameters[i].ParameterType;
                var desiredType = desired[i];

                if (desiredType == null)
                {
                    continue;
                }

                if (actualType == desiredType)
                {
                    continue;
                }

                if (desiredType.IsAssignableFrom(actualType) || actualType.IsAssignableFrom(desiredType))
                {
                    continue;
                }

                if (desiredType == typeof(List<Entity>) && IsEntityCollection(actualType))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private static bool IsEntityCollection(Type type)
        {
            if (type == null)
            {
                return false;
            }

            if (type.IsArray)
            {
                return typeof(Entity).IsAssignableFrom(type.GetElementType());
            }

            if (type.IsGenericType)
            {
                var genericArguments = type.GetGenericArguments();
                if (genericArguments.Length == 1 && typeof(Entity).IsAssignableFrom(genericArguments[0]))
                {
                    var genericDefinition = type.GetGenericTypeDefinition();
                    if (genericDefinition == typeof(List<>) ||
                        genericDefinition == typeof(IList<>) ||
                        genericDefinition == typeof(ICollection<>) ||
                        genericDefinition == typeof(IEnumerable<>))
                    {
                        return true;
                    }

                    if (genericDefinition.Name.Contains("List", StringComparison.OrdinalIgnoreCase) ||
                        genericDefinition.Name.Contains("Collection", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return typeof(IList).IsAssignableFrom(type) || typeof(ICollection).IsAssignableFrom(type);
        }

        private void RemoveNullEntities()
        {
            if (_entityBuffer.Count == 0)
            {
                return;
            }

            for (int i = _entityBuffer.Count - 1; i >= 0; i--)
            {
                if (_entityBuffer[i] == null)
                {
                    _entityBuffer.RemoveAt(i);
                }
            }
        }

        private static void LogCollectionMethod(string signature)
        {
            if (_loggedCollectionMethod)
            {
                return;
            }

            _loggedCollectionMethod = true;
            ModLogger.Info($"[ZombiesDeOP] DetectionSystemRuntime usando {signature} para obtener entidades en runtime");
        }

        private UIOverlayComponent EnsureOverlay()
        {
            if (_overlay != null)
            {
                return _overlay;
            }

            _overlay = GetComponent<UIOverlayComponent>();
            if (_overlay == null)
            {
                _overlay = VisibilityOverlaySystem.OverlayComponent;
            }

            if (_overlay == null)
            {
                _overlay = gameObject.AddComponent<UIOverlayComponent>();
                VisibilityOverlaySystem.Initialize(_overlay);
            }

            return _overlay;
        }

        internal static void NotifyHarmonyTick(EntityEnemy enemy)
        {
            if (enemy == null)
            {
                return;
            }

            if (Instance != null)
            {
                Instance.ProcessHarmonyObservation(enemy);
                return;
            }

            if (!_warnedMissingRuntime)
            {
                _warnedMissingRuntime = true;
                ModLogger.Warn("⚠️ [ZombiesDeOP] NotifyHarmonyTick sin runtime activo; usando fallback directo");
            }

            var world = GameManager.Instance?.World;
            var player = world?.GetPrimaryPlayer() as EntityPlayerLocal;
            if (player == null)
            {
                return;
            }

            var overlay = VisibilityOverlaySystem.OverlayComponent;
            float radius = GetFallbackRadius();
            DetectionSystem.ProcessHarmonyObservation(player, enemy, radius, overlay);
        }

        private void ProcessHarmonyObservation(EntityEnemy enemy)
        {
            var world = GameManager.Instance?.World;
            var player = world?.GetPrimaryPlayer() as EntityPlayerLocal;
            if (player == null)
            {
                return;
            }

            var overlay = EnsureOverlay();
            DetectionSystem.ProcessHarmonyObservation(player, enemy, _radius, overlay);
        }

        private static float GetFallbackRadius()
        {
            float configuredRadius = ModSettings.DetectionRange;
            return configuredRadius > 0f ? Mathf.Clamp(configuredRadius, 5f, 60f) : DEFAULT_RADIUS;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
