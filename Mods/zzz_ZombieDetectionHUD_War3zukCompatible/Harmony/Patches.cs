using HarmonyLib;

namespace ZombieDetectionHUD
{
    /// <summary>
    /// Harmony patches dedicated to monitoring core entity behaviours without conflicting with other mods.
    /// </summary>
    [HarmonyPatch]
    public static class DetectionPatches
    {
        [HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.CanSee))]
        [HarmonyPostfix]
        private static void EntityAlive_CanSee_Postfix(EntityAlive __instance, Entity target, ref bool __result)
        {
            if (__instance is not EntityEnemy || target is not EntityPlayerLocal)
            {
                return;
            }

            // Reserved for future compatibility hooks. Right now we simply log when debug mode is enabled.
            if (ModConfig.DebugMode)
            {
                ModLogger.Debug($"CanSee -> {__instance.EntityName} => {__result}");
            }
        }

        [HarmonyPatch(typeof(EntityEnemy), nameof(EntityEnemy.Update))]
        [HarmonyPostfix]
        private static void EntityEnemy_Update_Postfix(EntityEnemy __instance)
        {
            if (!ModConfig.DebugMode)
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
