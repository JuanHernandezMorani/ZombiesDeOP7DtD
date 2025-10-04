using System;
using System.Collections.Generic;
using UnityEngine;
using ZombiesDeOPUnified.Core;

namespace ZombiesDeOPUnified.Submodules.FarmingModule
{
    public sealed class LiteFarmingModule : MonoBehaviour, IModule
    {
        public bool IsEnabled => FarmingModuleConfig.EnableLiteFarming;
        public string ModuleName => "Lite Farming";

        public void InitializeModule()
        {
            enabled = IsEnabled;
            if (!IsEnabled)
            {
                return;
            }

            LiteFarmingRegistrar.Initialize();
            ModEvents.GameStartDone.RegisterHandler(OnGameStartDone);
            ModLogger.Log("LiteFarming inicializado");
        }

        public void Shutdown()
        {
            ModEvents.GameStartDone.UnregisterHandler(OnGameStartDone);
            enabled = false;
        }

        private void OnGameStartDone()
        {
            LiteFarmingRegistrar.RegisterDefaults();
        }
    }

    internal static class LiteFarmingRegistrar
    {
        private static readonly Dictionary<string, LiteFarmingPlantDefinition> Plants = new();
        private static readonly Dictionary<string, LiteFarmingStationDefinition> Stations = new();
        private static bool initialised;

        public static void Initialize()
        {
            if (initialised)
            {
                return;
            }

            initialised = true;
        }

        public static void RegisterDefaults()
        {
            RegisterPlant(new LiteFarmingPlantDefinition(
                "litefarmingCloverPlant",
                "resourceLiteFarmingCloverLeaf",
                "resourceLiteFarmingCloverSeed",
                8,
                4,
                48f));

            RegisterPlant(new LiteFarmingPlantDefinition(
                "litefarmingExoticRootPlant",
                "resourceLiteFarmingExoticRoot",
                "resourceLiteFarmingExoticRootCutting",
                6,
                5,
                60f));

            RegisterPlant(new LiteFarmingPlantDefinition(
                "litefarmingSubterraneanAlgae",
                "resourceLiteFarmingSubterraneanAlgae",
                "resourceLiteFarmingSubterraneanAlgaeSpore",
                4,
                7,
                54f));

            RegisterStation(new LiteFarmingStationDefinition(
                "blockLiteFarmingFermenter",
                "LiteFarming Fermenter",
                "liteFermenter"));

            RegisterStation(new LiteFarmingStationDefinition(
                "blockLiteFarmingSmoker",
                "LiteFarming Smoker",
                "liteSmoker"));

            RegisterStation(new LiteFarmingStationDefinition(
                "blockLiteFarmingSlowCooker",
                "LiteFarming Slow Cooker",
                "liteSlowCooker"));
        }

        public static void RegisterPlant(LiteFarmingPlantDefinition definition)
        {
            if (Plants.ContainsKey(definition.PrefabName))
            {
                ModLogger.Warning($"Plant {definition.PrefabName} already registered, skipping duplicate.");
                return;
            }

            Plants.Add(definition.PrefabName, definition);
        }

        public static bool TryGetPlant(BlockValue blockValue, out LiteFarmingPlantDefinition definition)
        {
            if (blockValue.Block == null)
            {
                definition = null;
                return false;
            }

            return Plants.TryGetValue(blockValue.Block.GetBlockName(), out definition);
        }
    }

    internal sealed class LiteFarmingPlantDefinition
    {
        public LiteFarmingPlantDefinition(string prefabName, string harvestItem, string seedItem, int requiredLight, int requiredMoisture, float growthHours)
        {
            PrefabName = prefabName;
            HarvestItem = harvestItem;
            SeedItem = seedItem;
            RequiredLight = requiredLight;
            RequiredMoisture = requiredMoisture;
            GrowthHours = growthHours;
        }

        public string PrefabName { get; }
        public string HarvestItem { get; }
        public string SeedItem { get; }
        public int RequiredLight { get; }
        public int RequiredMoisture { get; }
        public float GrowthHours { get; }
    }

    internal sealed class LiteFarmingStationDefinition
    {
        public LiteFarmingStationDefinition(string blockName, string displayName, string craftArea)
        {
            BlockName = blockName;
            DisplayName = displayName;
            CraftArea = craftArea;
        }

        public string BlockName { get; }
        public string DisplayName { get; }
        public string CraftArea { get; }
    }
}
