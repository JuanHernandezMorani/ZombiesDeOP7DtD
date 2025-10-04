using HarmonyLib;
using ZombiesDeOPUnified.Core;
using ZombiesDeOPUnified.Submodules.ZombieDetectionSystem;

namespace ZombiesDeOPUnified.HarmonyPatches
{
    [HarmonyPatch]
    public static class PlayerDetectionPatches
    {
        [HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.CanSee))]
        [HarmonyPostfix]
        private static void EntityAlive_CanSee_Postfix(EntityAlive __instance, Entity target, ref bool __result)
        {
            if (!DetectionConfig.EnableDetectionSystem)
            {
                return;
            }

            if (__instance is not EntityEnemy || target is not EntityPlayerLocal)
            {
                return;
            }

            if (DetectionConfig.DebugMode)
            {
                ModLogger.Debug($"CanSee -> {__instance.EntityName} => {__result}");
            }
        }

        [HarmonyPatch(typeof(EntityEnemy), nameof(EntityEnemy.Update))]
        [HarmonyPostfix]
        private static void EntityEnemy_Update_Postfix(EntityEnemy __instance)
        {
            if (!DetectionConfig.DebugMode)
            {
                return;
            }

            if (__instance == null || !__instance.IsAlive())
            {
                return;
            }

            ModLogger.Debug($"Actualizando enemigo {__instance.EntityName} (ID {__instance.entityId}).");
        }
    }
}
