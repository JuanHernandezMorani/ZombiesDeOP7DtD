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

        private static readonly MethodInfo EntitiesInBoundsWithBuffer = AccessTools.Method(
            typeof(World),
            "GetEntitiesInBounds",
            new[] { typeof(Type), typeof(Bounds), typeof(List<Entity>) });

        private static readonly MethodInfo EntitiesInBoundsReturningList = AccessTools.Method(
            typeof(World),
            "GetEntitiesInBounds",
            new[] { typeof(Type), typeof(Bounds) });

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

            _updateInterval = Mathf.Clamp(DEFAULT_INTERVAL, 0.1f, 1f);
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
                    object[] args = { null, bounds, _entityBuffer };
                    EntitiesInBoundsWithBuffer.Invoke(world, args);
                    LogCollectionMethod($"{EntitiesInBoundsWithBuffer.DeclaringType?.Name}.{EntitiesInBoundsWithBuffer.Name}(Type, Bounds, List<Entity>)");
                    return true;
                }

                if (EntitiesInBoundsReturningList != null)
                {
                    object[] args = { null, bounds };
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

                        LogCollectionMethod($"{EntitiesInBoundsReturningList.DeclaringType?.Name}.{EntitiesInBoundsReturningList.Name}(Type, Bounds)");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("❌ [ZombiesDeOP] Error obteniendo entidades cercanas", ex);
            }

            return false;
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
