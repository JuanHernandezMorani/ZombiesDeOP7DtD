using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using ZombiesDeOPUnified.Core;
using ZombiesDeOPUnified.Submodules.StorageAccessModule;

namespace ZombiesDeOPUnified.HarmonyPatches
{
    [HarmonyPatch]
    public static class StorageAccessPatches
    {
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            var typesToPatch = new List<Type> { typeof(TileEntityLootContainer) };

            var secureType = AccessTools.TypeByName("TileEntitySecureLootContainer");
            if (secureType != null)
            {
                typesToPatch.Add(secureType);
            }

            foreach (var candidateName in StorageAccessManager.War3zukContainerTypeNames)
            {
                var candidateType = AccessTools.TypeByName(candidateName);
                if (candidateType != null)
                {
                    typesToPatch.Add(candidateType);
                }
            }

            foreach (var type in typesToPatch)
            {
                var method = AccessTools.Method(type, "IsPlayerWithin");
                if (method != null)
                {
                    yield return method;
                }
            }
        }

        [HarmonyPrefix]
        private static bool Prefix(object __instance, ref bool __result, [HarmonyArgument(0)] object interactingEntity)
        {
            if (!StorageAccessConfig.EnableExtendedReach)
            {
                return true;
            }

            if (!StorageAccessManager.IsSupportedContainer(__instance))
            {
                return true;
            }

            if (StorageAccessManager.ShouldBypassForQuickStack())
            {
                return true;
            }

            var entity = StorageAccessManager.ResolveEntity(interactingEntity);
            if (entity == null)
            {
                return true;
            }

            var tileEntity = StorageAccessManager.ResolveTileEntity(__instance);
            if (tileEntity == null)
            {
                return true;
            }

            if (StorageAccessManager.HasExtendedAccess(entity, tileEntity))
            {
                __result = true;
                return false;
            }

            return true;
        }
    }

    internal static class StorageAccessManager
    {
        internal static readonly string[] War3zukContainerTypeNames =
        {
            "War3zuk.TileEntityLootContainerWar3zuk",
            "War3zuk.TileEntitySecureLootContainerWar3zuk",
            "TileEntityLootContainerWar3zuk",
            "TileEntitySecureLootContainerWar3zuk",
            "TileEntitySecureLootContainerUnlocked"
        };

        private static readonly Vector3 HalfBlock = new(0.5f, 0.5f, 0.5f);

        internal static bool IsSupportedContainer(object tileEntityObject)
        {
            if (tileEntityObject == null)
            {
                return false;
            }

            var type = tileEntityObject.GetType();
            if (typeof(TileEntityLootContainer).IsAssignableFrom(type))
            {
                return true;
            }

            var secureType = AccessTools.TypeByName("TileEntitySecureLootContainer");
            if (secureType != null && secureType.IsAssignableFrom(type))
            {
                return true;
            }

            foreach (var candidateName in War3zukContainerTypeNames)
            {
                var candidateType = AccessTools.TypeByName(candidateName);
                if (candidateType != null && candidateType.IsAssignableFrom(type))
                {
                    return true;
                }
            }

            return false;
        }

        internal static TileEntity ResolveTileEntity(object instance)
        {
            return instance as TileEntity;
        }

        internal static EntityAlive ResolveEntity(object interactingEntity)
        {
            return interactingEntity as EntityAlive;
        }

        internal static bool ShouldBypassForQuickStack()
        {
            var quickStackType = AccessTools.TypeByName("QuickStack.QuickStackManager");
            if (quickStackType == null)
            {
                return false;
            }

            try
            {
                var property = AccessTools.Property(quickStackType, "IsRunning") ?? AccessTools.Property(quickStackType, "Enabled");
                if (property != null && property.PropertyType == typeof(bool))
                {
                    var value = property.GetValue(null, null);
                    if (value is bool boolValue)
                    {
                        return boolValue;
                    }
                }

                var field = AccessTools.Field(quickStackType, "IsRunning") ?? AccessTools.Field(quickStackType, "s_isRunning");
                if (field != null && field.FieldType == typeof(bool))
                {
                    var value = field.GetValue(null);
                    if (value is bool boolField)
                    {
                        return boolField;
                    }
                }
            }
            catch (Exception exception)
            {
                ModLogger.Warning($"QuickStack detection failed: {exception.Message}");
            }

            return false;
        }

        internal static bool HasExtendedAccess(EntityAlive entity, TileEntity tileEntity)
        {
            if (entity == null || tileEntity == null)
            {
                return false;
            }

            float reachDistance = Mathf.Max(StorageAccessConfig.ReachDistance, 0f);
            if (reachDistance <= 0f)
            {
                return false;
            }

            Vector3 containerCenter = GetContainerWorldCenter(tileEntity);
            Vector3 entityPosition = entity.position;
            float distance = Vector3.Distance(entityPosition, containerCenter);

            if (distance <= StorageAccessConfig.MinimumVanillaDistance)
            {
                return true;
            }

            if (distance > reachDistance)
            {
                return false;
            }

            if (HasLineOfSight(entity, tileEntity, containerCenter))
            {
                return true;
            }

            return distance <= StorageAccessConfig.ProximityTolerance;
        }

        private static Vector3 GetContainerWorldCenter(TileEntity tileEntity)
        {
            Vector3 center;
            try
            {
                var worldPos = tileEntity.ToWorldPos();
                center = new Vector3(worldPos.x, worldPos.y, worldPos.z) + HalfBlock;
            }
            catch
            {
                center = tileEntity.localChunkPos + HalfBlock;
            }

            return center;
        }

        private static bool HasLineOfSight(EntityAlive entity, TileEntity tileEntity, Vector3 containerCenter)
        {
            try
            {
                var world = entity.world ?? GameManager.Instance?.World;
                if (world == null)
                {
                    return false;
                }

                Vector3 start = GetEyePosition(entity);
                Vector3 direction = containerCenter - start;
                float distance = direction.magnitude;
                if (distance <= 0.1f)
                {
                    return true;
                }

                int steps = Mathf.Clamp(Mathf.CeilToInt(distance * 4f), 1, 256);
                Vector3 step = direction / steps;
                var targetBlock = tileEntity.ToWorldPos();

                for (int i = 1; i < steps; i++)
                {
                    Vector3 sample = start + step * i;
                    var blockPos = new Vector3i(Mathf.FloorToInt(sample.x), Mathf.FloorToInt(sample.y), Mathf.FloorToInt(sample.z));
                    if (blockPos.Equals(targetBlock))
                    {
                        continue;
                    }

                    var blockValue = world.GetBlock(blockPos);
                    if (!IsPassable(blockValue))
                    {
                        return false;
                    }
                }

                return true;
            }
            catch (Exception exception)
            {
                ModLogger.Warning($"Line of sight check failed: {exception.Message}");
                return true;
            }
        }

        private static Vector3 GetEyePosition(EntityAlive entity)
        {
            float eyeHeight = 1.6f;
            try
            {
                var method = AccessTools.Method(entity.GetType(), "GetEyeHeight");
                if (method != null && method.ReturnType == typeof(float) && method.GetParameters().Length == 0)
                {
                    eyeHeight = (float)method.Invoke(entity, null);
                }
                else
                {
                    var field = AccessTools.Field(entity.GetType(), "eyeHeight");
                    if (field != null && field.FieldType == typeof(float))
                    {
                        eyeHeight = (float)field.GetValue(entity);
                    }
                }
            }
            catch (Exception exception)
            {
                ModLogger.Debug($"Failed to read eye height: {exception.Message}");
            }

            return entity.position + Vector3.up * Mathf.Max(1f, eyeHeight);
        }

        private static bool IsPassable(BlockValue blockValue)
        {
            try
            {
                if (blockValue.type == 0)
                {
                    return true;
                }

                var block = blockValue.Block;
                if (block == null)
                {
                    return true;
                }

                if (block.IsAir)
                {
                    return true;
                }

                var shape = block.shape;
                if (shape == null)
                {
                    return false;
                }

                var airFace = AccessTools.Method(shape.GetType(), "IsAirFace", new[] { typeof(int) });
                if (airFace != null)
                {
                    var result = airFace.Invoke(shape, new object[] { 0 });
                    if (result is bool boolResult)
                    {
                        return boolResult;
                    }
                }

                var isPassable = AccessTools.Method(shape.GetType(), "IsPassable", Type.EmptyTypes);
                if (isPassable != null)
                {
                    var result = isPassable.Invoke(shape, Array.Empty<object>());
                    if (result is bool boolResult)
                    {
                        return boolResult;
                    }
                }

                return false;
            }
            catch
            {
                return true;
            }
        }
    }
}
