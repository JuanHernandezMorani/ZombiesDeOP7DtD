using System;
using System.Reflection;
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
            ModLogger.Info("🧠 [ZombiesDeOP] Gestor de comportamiento inicializado");
        }

        public static void Shutdown()
        {
            initialized = false;
        }

        private static void ApplyBehaviorInsights(EntityAlive entity)
        {
            if (!initialized || entity == null)
            {
                return;
            }

            if (!entity.IsAlive())
            {
                return;
            }

            if (ModSettings.DebugMode)
            {
                ModLogger.LogDebug($"Refrescando IA para {entity.EntityName} ({entity.entityId})");
            }
        }

        internal static bool IsHostileEntity(EntityAlive entity)
        {
            if (entity == null)
            {
                return false;
            }

            if (entity is EntityZombie || entity is EntityEnemyAnimal || entity is EntityBandit)
            {
                return true;
            }

            var name = entity.GetType().Name;
            if (name.Contains("Enemy", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Zombie", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Bandit", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Spider", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Demon", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Mutant", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            try
            {
                var aiField = entity.GetType().GetField("aiTasks", BindingFlags.Instance | BindingFlags.NonPublic);
                if (aiField != null)
                {
                    var value = aiField.GetValue(entity)?.ToString();
                    if (!string.IsNullOrEmpty(value) && value.Contains("Attack", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // Ignorar cualquier error de reflexión: fallback seguro
            }

            return false;
        }

        [HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.OnAddedToWorld))]
        private static class EntitySpawnPatch
        {
            static EntitySpawnPatch()
            {
                ModLogger.Info("[ZombiesDeOP] Harmony patch target: EntityAlive.OnAddedToWorld (OK)");
            }

            private static void Postfix(EntityAlive __instance)
            {
                if (!IsHostileEntity(__instance))
                {
                    return;
                }

                ApplyBehaviorInsights(__instance);
            }
        }
    }
}
