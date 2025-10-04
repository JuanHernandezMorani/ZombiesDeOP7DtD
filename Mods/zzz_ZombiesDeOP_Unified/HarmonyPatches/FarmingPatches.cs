using HarmonyLib;
using System;
using UnityEngine;
using ZombiesDeOPUnified.Core;
using ZombiesDeOPUnified.Submodules.FarmingModule;

namespace ZombiesDeOPUnified.HarmonyPatches
{
    [HarmonyPatch(typeof(BlockPlantGrowing), nameof(BlockPlantGrowing.CheckPlantRequirements))]
    public static class LiteFarmingPlantRequirementPatch
    {
        private static readonly Vector3Int[] WaterCheckOffsets =
        {
            new Vector3Int(1, 0, 0),
            new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 0, 1),
            new Vector3Int(0, 0, -1),
            new Vector3Int(0, -1, 0)
        };

        private static void Postfix(ref bool __result, BlockValue blockValue, WorldBase world, Vector3i position, bool bFullQualityCheck)
        {
            if (!FarmingModuleConfig.EnableLiteFarming)
            {
                return;
            }

            if (!LiteFarmingRegistrar.TryGetPlant(blockValue, out var definition))
            {
                return;
            }

            try
            {
                var light = world.GetBlockLightValue(position);
                var hasRequiredLight = light >= definition.RequiredLight;

                var hasWater = false;
                foreach (var offset in WaterCheckOffsets)
                {
                    var neighborPos = position + new Vector3i(offset);
                    var neighbor = world.GetBlock(neighborPos);
                    if (neighbor.Block is BlockLiquidv2)
                    {
                        hasWater = true;
                        break;
                    }
                }

                var timePassed = world.worldTime - blockValue.meta;
                var growthHours = timePassed / 1000f;
                var hasTime = growthHours >= definition.GrowthHours;

                __result = hasRequiredLight && hasWater && hasTime;
            }
            catch (Exception e)
            {
                ModLogger.Error($"Failed to validate growth requirements for {definition.PrefabName}: {e}");
            }
        }
    }
}
