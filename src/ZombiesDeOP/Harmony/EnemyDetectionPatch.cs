using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using ZombiesDeOP.Systems;
using ZombiesDeOP.Utilities;

namespace ZombiesDeOP.Harmony
{
    [HarmonyPatch(typeof(EntityEnemy))]
    [HarmonyPatch("Update")]
    public static class EnemyDetectionPatch
    {
        private const float DetectionDistance = 10f;

        private static void Postfix(EntityEnemy __instance)
        {
            if (__instance == null)
            {
                return;
            }

            try
            {
                var overlay = VisibilityOverlaySystem.OverlayComponent;
                if (overlay == null)
                {
                    return;
                }

                var world = GameManager.Instance?.World;
                if (world == null)
                {
                    return;
                }

                var localPlayer = world.GetPrimaryPlayer() as EntityPlayerLocal;
                if (localPlayer == null)
                {
                    return;
                }

                bool isCrouching = ResolveCrouchState(localPlayer);
                float distanceToPlayer = __instance.GetDistance(localPlayer);
                bool isTargetingPlayer = __instance.GetAttackTarget() == localPlayer;

                ModLogger.Debug($"👁️ [ZombiesDeOP] Detección -> Enemigo: {__instance.EntityName}, Agachado: {isCrouching}, Distancia: {distanceToPlayer:F2}, Target: {isTargetingPlayer}");

                if (isTargetingPlayer)
                {
                    overlay.SetState("seen");
                    ModLogger.Log("👁️ [ZombiesDeOP] Estado detectado: SEEN");
                    return;
                }

                if (isCrouching && distanceToPlayer <= DetectionDistance)
                {
                    overlay.SetState("hidden");
                    ModLogger.Log("👁️ [ZombiesDeOP] Estado detectado: HIDDEN");
                }
                else if (distanceToPlayer > DetectionDistance)
                {
                    overlay.SetState("none");
                    ModLogger.Debug("👁️ [ZombiesDeOP] Estado detectado: NONE (fuera de rango)");
                }
                else
                {
                    overlay.SetState("none");
                    ModLogger.Debug("👁️ [ZombiesDeOP] Estado detectado: NONE (sin condiciones)");
                }
            }
            catch (Exception e)
            {
                ModLogger.Error($"❌ [ZombiesDeOP] Error en EnemyDetectionPatch: {e}");
            }
        }

        private static bool ResolveCrouchState(EntityPlayerLocal player)
        {
            if (player == null)
            {
                return false;
            }

            try
            {
                var type = player.GetType();
                var property = AccessTools.Property(type, "IsCrouching") ?? AccessTools.Property(type, "Crouching");
                if (property != null)
                {
                    if (property.GetValue(player) is bool propertyValue)
                    {
                        return propertyValue;
                    }
                }

                var field = AccessTools.Field(type, "isCrouching") ?? AccessTools.Field(type, "Crouching");
                if (field != null && field.GetValue(player) is bool fieldValue)
                {
                    return fieldValue;
                }
            }
            catch (TargetInvocationException e)
            {
                ModLogger.Debug($"👁️ [ZombiesDeOP] Error evaluando crouch (invocación): {e.InnerException ?? e}");
            }
            catch (Exception e)
            {
                ModLogger.Debug($"👁️ [ZombiesDeOP] Error evaluando crouch: {e}");
            }

            return false;
        }
    }
}
