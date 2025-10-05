using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using ZombiesDeOP.Utilities;

namespace ZombiesDeOP.Systems
{
    public static class DetectionSystem
    {
        private static readonly HashSet<int> ActiveDetections = new();
        private static bool initialized;

        public static void Initialize()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            ModLogger.Info("🔍 [ZombiesDeOP] Sistema de detección inicializado");
        }

        public static void Shutdown()
        {
            ActiveDetections.Clear();
            initialized = false;
        }

        private static void HandleZombieDetection(EntityEnemy enemy)
        {
            if (!initialized || enemy == null)
            {
                return;
            }

            if (!enemy.IsAlive())
            {
                return;
            }

            var player = GameManager.Instance?.World?.GetPrimaryPlayer();
            if (player == null)
            {
                return;
            }

            float distance = enemy.GetDistance(player);
            if (distance <= ModSettings.DetectionRange)
            {
                if (ActiveDetections.Add(enemy.entityId))
                {
                    ModLogger.LogDebug($"Detección registrada para {enemy.EntityName} a {distance:F1}m");
                    HUDManager.ReportDetection(enemy, true, distance);
                }
            }
            else if (ActiveDetections.Remove(enemy.entityId))
            {
                HUDManager.ReportDetection(enemy, false, distance);
            }
        }

        [HarmonyPatch(typeof(EntityEnemy))]
        [HarmonyPatch("Update")]
        private static class EntityEnemyDetectionPatch
        {
            private static void Postfix(EntityEnemy __instance)
            {
                if (__instance == null)
                {
                    return;
                }

                var player = GameManager.Instance?.World?.GetPrimaryPlayer();
                if (player == null)
                {
                    return;
                }

                float distanceToPlayer = __instance.GetDistance(player);
                bool canSeePlayer = __instance.CanSee(player);
                bool canHearPlayer = distanceToPlayer <= __instance.GetSeeDistance() * 0.75f;
                bool canDetectPlayer = canSeePlayer || canHearPlayer;

                if (canDetectPlayer)
                {
                    HandleZombieDetection(__instance);
                }
                else if (ActiveDetections.Remove(__instance.entityId))
                {
                    HUDManager.ReportDetection(__instance, false, distanceToPlayer);
                }
            }
        }
    }
}
