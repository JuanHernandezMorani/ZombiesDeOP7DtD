using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using ZombiesDeOP.Utilities;

namespace ZombiesDeOP.Systems
{
    public enum DetectionState
    {
        None,
        Hidden,
        Seen
    }

    public static class DetectionSystem
    {
        private sealed class EnemySnapshot
        {
            public EnemySnapshot(EntityEnemy enemy, EntityPlayerLocal player, float radius)
            {
                Enemy = enemy;
                EntityId = enemy?.entityId ?? -1;
                Timestamp = Time.time;
                Distance = enemy?.GetDistance(player) ?? float.MaxValue;
                bool isTargeting = enemy != null && enemy.GetAttackTarget() == player;
                bool canSee = DetectionHelpers.TryCanSee(enemy, player);
                Seen = isTargeting || canSee;
                HiddenCandidate = !Seen && Distance <= radius;
            }

            public EntityEnemy Enemy { get; }
            public int EntityId { get; }
            public float Timestamp { get; }
            public float Distance { get; }
            public bool Seen { get; }
            public bool HiddenCandidate { get; }
            public bool IsValid => Enemy != null && Enemy.IsAlive();
        }

        private static readonly Dictionary<int, EnemySnapshot> EnemyObservations = new Dictionary<int, EnemySnapshot>();
        private const float HUD_COOLDOWN = 1.25f;
        private const float SNAPSHOT_TTL = 1.5f;

        private static DetectionState currentState = DetectionState.None;
        private static bool initialized;
        private static float lastHudMessageTime;
        private static float lastStateChangeTime;

        public static DetectionState CurrentState => currentState;

        public static void Initialize()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            currentState = DetectionState.None;
            EnemyObservations.Clear();
            lastHudMessageTime = 0f;
            lastStateChangeTime = Time.time;
            ModLogger.Info("🔍 [ZombiesDeOP] Sistema de detección inicializado");
        }

        public static void Shutdown()
        {
            EnemyObservations.Clear();
            initialized = false;
            currentState = DetectionState.None;
            lastHudMessageTime = 0f;
            lastStateChangeTime = 0f;
        }

        internal static void ProcessRuntimeTick(EntityPlayerLocal player, IList<EntityEnemy> enemies, float radius, UIOverlayComponent overlay)
        {
            if (!initialized || player == null)
            {
                return;
            }

            bool crouching = DetectionHelpers.ResolveCrouchState(player);
            bool seen = false;
            bool hiddenCandidate = false;
            EntityEnemy seenEnemy = null;
            EntityEnemy hiddenEnemy = null;
            float seenDistance = float.MaxValue;
            float hiddenDistance = float.MaxValue;
            int evaluated = 0;

            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.IsAlive())
                {
                    continue;
                }

                evaluated++;
                float distance = enemy.GetDistance(player);
                bool isTargeting = enemy.GetAttackTarget() == player;
                bool canSee = DetectionHelpers.TryCanSee(enemy, player);

                if (isTargeting || canSee)
                {
                    seen = true;
                    if (distance < seenDistance)
                    {
                        seenDistance = distance;
                        seenEnemy = enemy;
                    }
                }
                else if (distance <= radius)
                {
                    hiddenCandidate = true;
                    if (distance < hiddenDistance)
                    {
                        hiddenDistance = distance;
                        hiddenEnemy = enemy;
                    }
                }

                EnemyObservations[enemy.entityId] = new EnemySnapshot(enemy, player, radius);
            }

            CleanupExpiredSnapshots();

            bool hidden = !seen && crouching && hiddenCandidate && hiddenEnemy != null && hiddenDistance <= radius;
            var state = seen ? DetectionState.Seen : hidden ? DetectionState.Hidden : DetectionState.None;
            float referenceDistance = seen ? seenDistance : hiddenDistance;
            var referenceEnemy = seen ? seenEnemy : hiddenEnemy;

            ApplyState(state, referenceEnemy, referenceDistance, evaluated, overlay);
        }

        internal static void ProcessHarmonyObservation(EntityPlayerLocal player, EntityEnemy enemy, float radius, UIOverlayComponent overlay)
        {
            if (!initialized || player == null || enemy == null)
            {
                return;
            }

            EnemyObservations[enemy.entityId] = new EnemySnapshot(enemy, player, radius);
            CleanupExpiredSnapshots();

            bool crouching = DetectionHelpers.ResolveCrouchState(player);
            bool anySeen = false;
            bool hiddenCandidate = false;
            EntityEnemy seenEnemy = null;
            EntityEnemy hiddenEnemy = null;
            float seenDistance = float.MaxValue;
            float hiddenDistance = float.MaxValue;
            int evaluated = 0;

            foreach (var pair in EnemyObservations.ToArray())
            {
                var snapshot = pair.Value;
                if (snapshot == null || !snapshot.IsValid)
                {
                    EnemyObservations.Remove(pair.Key);
                    continue;
                }

                evaluated++;
                if (snapshot.Seen)
                {
                    anySeen = true;
                    if (snapshot.Distance < seenDistance)
                    {
                        seenDistance = snapshot.Distance;
                        seenEnemy = snapshot.Enemy;
                    }
                }
                else if (snapshot.HiddenCandidate && snapshot.Distance < hiddenDistance)
                {
                    hiddenCandidate = true;
                    hiddenDistance = snapshot.Distance;
                    hiddenEnemy = snapshot.Enemy;
                }
            }

            bool hidden = !anySeen && crouching && hiddenCandidate && hiddenEnemy != null && hiddenDistance <= radius;
            var state = anySeen ? DetectionState.Seen : hidden ? DetectionState.Hidden : DetectionState.None;
            float referenceDistance = anySeen ? seenDistance : hiddenDistance;
            var referenceEnemy = anySeen ? seenEnemy : hiddenEnemy;

            ApplyState(state, referenceEnemy, referenceDistance, evaluated, overlay);
        }

        internal static void ResetState(UIOverlayComponent overlay)
        {
            if (!initialized || currentState == DetectionState.None)
            {
                return;
            }

            currentState = DetectionState.None;
            EnemyObservations.Clear();
            lastStateChangeTime = Time.time;
            overlay?.SetState("none");
            ModLogger.Info("👁️ [ZombiesDeOP] Estado de detección -> NONE (reinicio por falta de datos)");
        }

        private static void ApplyState(DetectionState state, EntityEnemy enemy, float distance, int evaluated, UIOverlayComponent overlay)
        {
            if (!initialized)
            {
                return;
            }

            if (state == currentState)
            {
                return;
            }

            currentState = state;
            lastStateChangeTime = Time.time;

            string overlayState = state switch
            {
                DetectionState.Seen => "seen",
                DetectionState.Hidden => "hidden",
                _ => "none"
            };

            overlay?.SetState(overlayState);

            string stateLabel = state.ToString().ToUpperInvariant();
            if (enemy != null && distance > 0f && distance < float.MaxValue)
            {
                ModLogger.Info($"👁️ [ZombiesDeOP] Estado de detección -> {stateLabel} (enemigos evaluados: {evaluated}, referencia: {enemy.EntityName}, distancia: {distance:F1}m)");
            }
            else
            {
                ModLogger.Info($"👁️ [ZombiesDeOP] Estado de detección -> {stateLabel} (enemigos evaluados: {evaluated})");
            }

            TryReportHud(state, enemy, distance);
        }

        private static void TryReportHud(DetectionState state, EntityEnemy enemy, float distance)
        {
            if (enemy == null)
            {
                return;
            }

            float now = Time.time;
            if (now - lastHudMessageTime < HUD_COOLDOWN)
            {
                return;
            }

            bool detected = state == DetectionState.Seen;
            HUDManager.ReportDetection(enemy, detected, distance);
            lastHudMessageTime = now;
        }

        private static void CleanupExpiredSnapshots()
        {
            if (EnemyObservations.Count == 0)
            {
                return;
            }

            float threshold = Time.time - SNAPSHOT_TTL;
            var expired = EnemyObservations
                .Where(pair => pair.Value == null || pair.Value.Timestamp < threshold || !pair.Value.IsValid)
                .Select(pair => pair.Key)
                .ToList();

            foreach (int key in expired)
            {
                EnemyObservations.Remove(key);
            }
        }

        private static class DetectionHelpers
        {
            private static readonly List<Func<EntityPlayerLocal, bool?>> CrouchResolvers = new List<Func<EntityPlayerLocal, bool?>>();
            private static readonly MethodInfo CanSeeMethod;

            static DetectionHelpers()
            {
                RegisterPropertyGetter("IsCrouching");
                RegisterPropertyGetter("Crouching");
                RegisterFieldGetter("isCrouching");
                RegisterFieldGetter("crouching");

                var enemyType = typeof(EntityEnemy);
                var candidateParams = new[] { typeof(EntityAlive) };
                CanSeeMethod = AccessTools.Method(enemyType, "CanSee", candidateParams);
                if (CanSeeMethod == null)
                {
                    CanSeeMethod = AccessTools.Method(enemyType, "CanSee", new[] { typeof(Entity) });
                }
            }

            public static bool ResolveCrouchState(EntityPlayerLocal player)
            {
                if (player == null)
                {
                    return false;
                }

                try
                {
                    foreach (var resolver in CrouchResolvers)
                    {
                        bool? crouching = resolver(player);
                        if (crouching.HasValue)
                        {
                            return crouching.Value;
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.LogDebug($"ResolveCrouchState fallback: {ex.Message}");
                }

                return false;
            }

            public static bool TryCanSee(EntityEnemy enemy, EntityPlayerLocal player)
            {
                if (enemy == null || player == null)
                {
                    return false;
                }

                try
                {
                    if (CanSeeMethod != null)
                    {
                        object result = CanSeeMethod.Invoke(enemy, new object[] { player });
                        if (result is bool seen)
                        {
                            return seen;
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.LogDebug($"CanSee reflection fallback: {ex.Message}");
                }

                return EstimateLineOfSight(enemy, player);
            }

            private static bool EstimateLineOfSight(EntityEnemy enemy, EntityPlayerLocal player)
            {
                Vector3 enemyEye = GetEyePosition(enemy);
                Vector3 playerEye = GetEyePosition(player);
                Vector3 direction = playerEye - enemyEye;
                float distance = direction.magnitude;
                if (distance <= 0.01f)
                {
                    return true;
                }

                direction /= distance;
                if (!Physics.Raycast(enemyEye, direction, out RaycastHit hit, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    return true;
                }

                return hit.distance >= distance - 0.25f;
            }

            private static Vector3 GetEyePosition(EntityAlive entity)
            {
                if (entity == null)
                {
                    return Vector3.zero;
                }

                Vector3 basePosition = entity.position;
                float eyeHeight = 1.6f;

                try
                {
                    var method = AccessTools.Method(entity.GetType(), "GetEyeHeight");
                    if (method != null)
                    {
                        object result = method.Invoke(entity, Array.Empty<object>());
                        if (result is float height && height > 0f)
                        {
                            eyeHeight = height;
                        }
                    }
                }
                catch
                {
                    // Ignorar errores de reflexión, usar altura por defecto
                }

                return basePosition + Vector3.up * eyeHeight;
            }

            private static void RegisterPropertyGetter(string name)
            {
                var property = AccessTools.Property(typeof(EntityPlayerLocal), name);
                if (property == null || property.PropertyType != typeof(bool))
                {
                    return;
                }

                var getter = property.GetGetMethod(true);
                if (getter == null)
                {
                    return;
                }

                CrouchResolvers.Add(player =>
                {
                    if (player == null)
                    {
                        return null;
                    }

                    object value = getter.Invoke(player, null);
                    return value is bool crouching ? crouching : (bool?)null;
                });
            }

            private static void RegisterFieldGetter(string name)
            {
                var field = AccessTools.Field(typeof(EntityPlayerLocal), name);
                if (field == null || field.FieldType != typeof(bool))
                {
                    return;
                }

                CrouchResolvers.Add(player =>
                {
                    if (player == null)
                    {
                        return null;
                    }

                    object value = field.GetValue(player);
                    return value is bool crouching ? crouching : (bool?)null;
                });
            }
        }
    }
}
