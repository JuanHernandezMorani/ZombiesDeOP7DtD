using HarmonyLib;
using ZombiesDeOP.Utilities;

namespace ZombiesDeOP.Systems
{
    public static class BehaviorManager
    {
        private static bool initialized;

        public static void Initialize()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            ModLogger.Log("🧠 [ZombiesDeOP] Gestor de comportamiento inicializado");
        }

        public static void Shutdown()
        {
            initialized = false;
        }

        private static void ApplyBehaviorInsights(EntityEnemy enemy)
        {
            if (!initialized || enemy == null)
            {
                return;
            }

            if (!enemy.IsAlive())
            {
                return;
            }

            if (ModSettings.DebugMode)
            {
                ModLogger.Debug($"Refrescando IA para {enemy.EntityName} ({enemy.entityId})");
            }
        }

        [HarmonyPatch(typeof(EntityEnemy))]
        [HarmonyPatch("OnAddedToWorld")]
        private static class EntityEnemySpawnPatch
        {
            private static void Postfix(EntityEnemy __instance)
            {
                ApplyBehaviorInsights(__instance);
            }
        }
    }
}
