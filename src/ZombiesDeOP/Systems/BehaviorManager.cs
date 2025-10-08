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
            if (name.IndexOf("Enemy", StringComparison.OrdinalIgnoreCase) >= 0||
                name.IndexOf("Zombie", StringComparison.OrdinalIgnoreCase) >= 0||
                name.IndexOf("Bandit", StringComparison.OrdinalIgnoreCase) >= 0||
                name.IndexOf("Spider", StringComparison.OrdinalIgnoreCase) >= 0||
                name.IndexOf("Demon", StringComparison.OrdinalIgnoreCase) >= 0||
                name.IndexOf("Mutant", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            try
            {
                var aiField = entity.GetType().GetField("aiTasks", BindingFlags.Instance | BindingFlags.NonPublic);
                if (aiField != null)
                {
                    var value = aiField.GetValue(entity)?.ToString();
                    if (!string.IsNullOrEmpty(value) && (value.IndexOf("Attack", StringComparison.OrdinalIgnoreCase) >= 0))
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
