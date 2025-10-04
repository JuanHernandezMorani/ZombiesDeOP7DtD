using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using ZombiesDeOPUnified.Core;
using ZombiesDeOPUnified.Submodules.AmbientAudioModule;
using ZombiesDeOPUnified.Submodules.FarmingModule;
using ZombiesDeOPUnified.Submodules.InventoryEnhancements;
using ZombiesDeOPUnified.Submodules.StorageAccessModule;
using ZombiesDeOPUnified.Submodules.VisualWeatherModule;
using ZombiesDeOPUnified.Submodules.ZombieBehaviorModule;
using ZombiesDeOPUnified.Submodules.ZombieDetectionSystem;

namespace ZombiesDeOPUnified
{
    public class ZombiesDeOPMain : IMod
    {
        private static GameObject modGameObject;
        private static Harmony harmonyInstance;
        private static readonly List<IModule> activeModules = new();

        public void Start()
        {
            try
            {
                ModLogger.Log("Iniciando ZombiesDeOP Unified Mod");

                ModConfig.Load();

                harmonyInstance = new Harmony("com.juanhernandez.zombiesdeop.unified");
                harmonyInstance.PatchAll(Assembly.GetExecutingAssembly());

                modGameObject = new GameObject("ZombiesDeOP_Runtime");
                GameObject.DontDestroyOnLoad(modGameObject);

                CompatibilityManager.ApplyCompatibilityPatches();

                InitializeSubmodules();

                ModLogger.Log("ZombiesDeOP Unified Mod cargado exitosamente");
            }
            catch (Exception e)
            {
                ModLogger.Error($"Error crítico durante la inicialización: {e}");
            }
        }

        public void Stop()
        {
            try
            {
                for (int i = activeModules.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        activeModules[i]?.Shutdown();
                    }
                    catch (Exception exception)
                    {
                        ModLogger.Error($"Error al cerrar el módulo {activeModules[i]?.ModuleName}: {exception}");
                    }
                }

                activeModules.Clear();

                harmonyInstance?.UnpatchAll(harmonyInstance.Id);
                harmonyInstance = null;

                if (modGameObject != null)
                {
                    UnityEngine.Object.Destroy(modGameObject);
                    modGameObject = null;
                }

                ModLogger.Log("ZombiesDeOP Unified Mod descargado exitosamente");
            }
            catch (Exception e)
            {
                ModLogger.Error($"Error durante la descarga: {e}");
            }
        }

        private void InitializeSubmodules()
        {
            activeModules.Clear();

            if (DetectionConfig.EnableDetectionSystem)
            {
                var detectionEngine = modGameObject.AddComponent<DetectionEngine>();
                detectionEngine.InitializeModule();
                activeModules.Add(detectionEngine);

                if (DetectionConfig.EnableDetectionHUD)
                {
                    var hudManager = modGameObject.AddComponent<DetectionHUDManager>();
                    hudManager.InitializeModule(detectionEngine);
                    activeModules.Add(hudManager);
                }
            }

            if (BehaviorConfig.EnableDepthAwareness)
            {
                var behaviorModule = modGameObject.AddComponent<ZombieBehaviorManager>();
                behaviorModule.InitializeModule();
                activeModules.Add(behaviorModule);
            }

            if (InventoryEnhancementConfig.EnableInventoryButtons)
            {
                var inventoryModule = modGameObject.AddComponent<InventoryButtonsModule>();
                inventoryModule.InitializeModule();
                activeModules.Add(inventoryModule);
            }

            if (StorageAccessConfig.EnableExtendedReach)
            {
                var storageModule = modGameObject.AddComponent<StorageAccessModule>();
                storageModule.InitializeModule();
                activeModules.Add(storageModule);
            }

            if (AmbientAudioConfig.EnableAmbientAudio)
            {
                var ambientModule = modGameObject.AddComponent<AmbientAudioRuntime>();
                ambientModule.InitializeModule();
                activeModules.Add(ambientModule);
            }

            if (FarmingModuleConfig.EnableLiteFarming)
            {
                var farmingModule = modGameObject.AddComponent<LiteFarmingModule>();
                farmingModule.InitializeModule();
                activeModules.Add(farmingModule);
            }

            if (VisualWeatherConfig.EnableVisualWeather)
            {
                var weatherModule = modGameObject.AddComponent<VisualWeatherModuleBehaviour>();
                weatherModule.InitializeModule();
                activeModules.Add(weatherModule);
            }

            ModLogger.Log($"Submódulos inicializados: {activeModules.Count}");
        }
    }

    public interface IModule
    {
        bool IsEnabled { get; }
        string ModuleName { get; }
        void InitializeModule();
        void Shutdown();
    }
}
