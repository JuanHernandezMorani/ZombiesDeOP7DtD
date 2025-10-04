using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace NoDigZombies
{
    /// <summary>
    /// Adjusts zombie perception when the tracked player is underground.
    /// </summary>
    [HarmonyPatch]
    public static class DepthSensePatch
    {
        private static readonly ConditionalWeakTable<EntitySenses, SenseSnapshot> Snapshots = new();

        private static readonly EnumEntitySenseType[] PerceptionChannels =
        {
            EnumEntitySenseType.Sight,
            EnumEntitySenseType.Sound,
            EnumEntitySenseType.Heat,
            EnumEntitySenseType.Light
        };

        private static readonly string[] HorizontalRangeFieldNames =
        {
            "maxViewDistance",
            "maxViewDistanceXZ",
            "maxTrackingDistance",
            "maxSenseDistanceXZ",
            "maxSightDistance"
        };

        [HarmonyPatch(typeof(EntityAlive), "OnUpdateLive")]
        [HarmonyPostfix]
        public static void ApplyDepthModifiers(EntityAlive __instance)
        {
            if (__instance == null)
            {
                return;
            }

            if (__instance is not EntityZombie)
            {
                return;
            }

            var senses = __instance?.Senses;
            if (senses == null)
            {
                return;
            }

            var world = __instance.world ?? GameManager.Instance?.World;
            if (world == null)
            {
                return;
            }

            var player = GetRelevantPlayer(world, __instance.position);
            if (player == null)
            {
                return;
            }

            var snapshot = Snapshots.GetValue(senses, SenseSnapshot.Create);

            float depth = CalculateDepth(world, player.position);
            float perceptionMultiplier = EvaluatePerceptionMultiplier(depth);
            float horizontalFactor = EvaluateHorizontalFactor(depth);

            foreach (var channel in PerceptionChannels)
            {
                ApplyMultiplierToSense(senses, channel, perceptionMultiplier, snapshot);
            }

            ApplyHorizontalRangeModifier(senses, horizontalFactor, snapshot);
        }

        private static EntityPlayer GetRelevantPlayer(World world, Vector3 referencePosition)
        {
            var player = world?.GetPrimaryPlayer();
            if (player != null)
            {
                return player;
            }

            if (world == null)
            {
                return null;
            }

            EntityPlayer closestPlayer = null;
            float closestDistanceSq = float.MaxValue;

            var players = world.GetPlayers();
            if (players == null)
            {
                return null;
            }

            foreach (var candidate in players)
            {
                if (candidate == null)
                {
                    continue;
                }

                float distanceSq = (candidate.position - referencePosition).sqrMagnitude;
                if (distanceSq < closestDistanceSq)
                {
                    closestDistanceSq = distanceSq;
                    closestPlayer = candidate;
                }
            }

            return closestPlayer;
        }

        private static float CalculateDepth(World world, Vector3 playerPosition)
        {
            if (world == null)
            {
                return 0f;
            }

            int surfaceX = Mathf.FloorToInt(playerPosition.x);
            int surfaceZ = Mathf.FloorToInt(playerPosition.z);
            float surfaceY = world.GetHeight(surfaceX, surfaceZ);
            float depth = surfaceY - playerPosition.y;
            return depth < 0f ? 0f : depth;
        }

        private static float EvaluatePerceptionMultiplier(float depth)
        {
            if (depth >= 22f)
            {
                return 0f;
            }

            if (depth >= 20f)
            {
                return 0.1f;
            }

            if (depth >= 15f)
            {
                return 0.3f;
            }

            if (depth >= 10f)
            {
                return 0.6f;
            }

            if (depth >= 5f)
            {
                return 0.8f;
            }

            return 1f;
        }

        private static float EvaluateHorizontalFactor(float depth)
        {
            if (depth >= 20f)
            {
                return 0f;
            }

            if (depth >= 10f)
            {
                return 0.5f;
            }

            return 1f;
        }

        private static void ApplyMultiplierToSense(EntitySenses senses, EnumEntitySenseType senseType, float multiplier, SenseSnapshot snapshot)
        {
            if (senses == null)
            {
                return;
            }

            object collection = AccessTools.Field(typeof(EntitySenses), "senseMap")?.GetValue(senses) ??
                               AccessTools.Field(typeof(EntitySenses), "senses")?.GetValue(senses);

            if (collection is not IDictionary dictionary)
            {
                return;
            }

            if (!dictionary.Contains(senseType))
            {
                return;
            }

            var senseEntry = dictionary[senseType];
            if (senseEntry == null)
            {
                return;
            }

            if (!snapshot.BaseRanges.TryGetValue(senseType, out var baseRange))
            {
                baseRange = ReadRangeValue(senseEntry);
                if (!float.IsNaN(baseRange) && baseRange > 0f)
                {
                    snapshot.BaseRanges[senseType] = baseRange;
                }
            }

            if (baseRange > 0f)
            {
                WriteRangeValue(senseEntry, baseRange * multiplier);
            }

            WriteMultiplierValue(senseEntry, multiplier);
        }

        private static void ApplyHorizontalRangeModifier(EntitySenses senses, float multiplier, SenseSnapshot snapshot)
        {
            if (senses == null)
            {
                return;
            }

            foreach (var fieldName in HorizontalRangeFieldNames)
            {
                var field = AccessTools.Field(typeof(EntitySenses), fieldName);
                if (field == null)
                {
                    continue;
                }

                if (!snapshot.HorizontalRanges.TryGetValue(fieldName, out var baseValue))
                {
                    if (field.GetValue(senses) is float currentValue && currentValue > 0f)
                    {
                        baseValue = currentValue;
                        snapshot.HorizontalRanges[fieldName] = baseValue;
                    }
                }

                if (baseValue <= 0f)
                {
                    continue;
                }

                field.SetValue(senses, baseValue * multiplier);
            }
        }

        private static float ReadRangeValue(object senseEntry)
        {
            if (senseEntry == null)
            {
                return float.NaN;
            }

            var binding = AccessTools.all;
            foreach (var fieldName in new[] { "maxRange", "maxDistance", "maxViewDistance", "distance" })
            {
                var field = AccessTools.Field(senseEntry.GetType(), fieldName);
                if (field != null && field.FieldType == typeof(float))
                {
                    return (float)field.GetValue(senseEntry);
                }
            }

            foreach (var propertyName in new[] { "MaxRange", "MaxDistance", "Range", "Distance" })
            {
                var property = AccessTools.Property(senseEntry.GetType(), propertyName);
                if (property != null && property.PropertyType == typeof(float) && property.CanRead)
                {
                    var value = property.GetValue(senseEntry, null);
                    if (value is float floatValue)
                    {
                        return floatValue;
                    }
                }
            }

            return float.NaN;
        }

        private static void WriteRangeValue(object senseEntry, float value)
        {
            if (senseEntry == null)
            {
                return;
            }

            foreach (var fieldName in new[] { "maxRange", "maxDistance", "maxViewDistance", "distance" })
            {
                var field = AccessTools.Field(senseEntry.GetType(), fieldName);
                if (field != null && field.FieldType == typeof(float))
                {
                    field.SetValue(senseEntry, value);
                    return;
                }
            }

            foreach (var propertyName in new[] { "MaxRange", "MaxDistance", "Range", "Distance" })
            {
                var property = AccessTools.Property(senseEntry.GetType(), propertyName);
                if (property != null && property.PropertyType == typeof(float) && property.CanWrite)
                {
                    property.SetValue(senseEntry, value, null);
                    return;
                }
            }
        }

        private static void WriteMultiplierValue(object senseEntry, float multiplier)
        {
            if (senseEntry == null)
            {
                return;
            }

            foreach (var fieldName in new[] { "perceptionMultiplier", "multiplier", "senseMultiplier" })
            {
                var field = AccessTools.Field(senseEntry.GetType(), fieldName);
                if (field != null && field.FieldType == typeof(float))
                {
                    field.SetValue(senseEntry, multiplier);
                    return;
                }
            }

            foreach (var propertyName in new[] { "PerceptionMultiplier", "Multiplier", "SenseMultiplier" })
            {
                var property = AccessTools.Property(senseEntry.GetType(), propertyName);
                if (property != null && property.PropertyType == typeof(float) && property.CanWrite)
                {
                    property.SetValue(senseEntry, multiplier, null);
                    return;
                }
            }
        }

        private sealed class SenseSnapshot
        {
            public readonly Dictionary<EnumEntitySenseType, float> BaseRanges = new();
            public readonly Dictionary<string, float> HorizontalRanges = new();

            public static SenseSnapshot Create(EntitySenses senses)
            {
                return new SenseSnapshot();
            }
        }
    }
}
