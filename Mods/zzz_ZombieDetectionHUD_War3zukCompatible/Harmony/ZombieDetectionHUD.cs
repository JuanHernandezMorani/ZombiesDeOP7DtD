using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ZombieDetectionHUD
{
    /// <summary>
    /// Entry point for the mod. Responsible for loading configuration, initializing Harmony patches and spawning runtime components.
    /// </summary>
    public sealed class ZombieDetectionHUD : IMod
    {
        private Harmony harmonyInstance;
        private GameObject modObject;
        private DetectionSystem detectionSystem;
        private HUDManager hudManager;

        /// <summary>
        /// Called by the game when the mod is loaded.
        /// </summary>
        public void Start()
        {
            try
            {
                ModLogger.Info("Iniciando ZombieDetectionHUD...");
                ModConfig.LoadConfig();
                ModCompatibility.Refresh();

                harmonyInstance = new Harmony("com.juanhernandez.zombiedetection");
                harmonyInstance.PatchAll(Assembly.GetExecutingAssembly());

                modObject = new GameObject("ZombieDetectionHUD_Object");
                GameObject.DontDestroyOnLoad(modObject);

                detectionSystem = modObject.AddComponent<DetectionSystem>();
                hudManager = modObject.AddComponent<HUDManager>();

                hudManager.Initialize(detectionSystem);
                detectionSystem.Initialize(hudManager);

                ModLogger.Info("ZombieDetectionHUD cargado exitosamente.");
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error cargando ZombieDetectionHUD.", ex);
            }
        }

        /// <summary>
        /// Called by the game when the mod is being unloaded.
        /// </summary>
        public void Stop()
        {
            try
            {
                if (modObject != null)
                {
                    UnityEngine.Object.Destroy(modObject);
                    modObject = null;
                }

                if (harmonyInstance != null)
                {
                    harmonyInstance.UnpatchAll(harmonyInstance.Id);
                    harmonyInstance = null;
                }

                ModLogger.Info("ZombieDetectionHUD descargado exitosamente.");
            }
            catch (Exception ex)
            {
                ModLogger.Error("Error descargando ZombieDetectionHUD.", ex);
            }
        }
    }

    /// <summary>
    /// Handles compatibility checks against War3zuk AIO Overhaul and logs the outcome for debugging.
    /// </summary>
    internal static class ModCompatibility
    {
        public static bool IsWar3zukLoaded { get; private set; }

        public static void Refresh()
        {
            try
            {
                var type = AccessTools.TypeByName("War3zukAIO.War3zukMain") ?? AccessTools.TypeByName("War3zukAIO.War3zukManager");
                IsWar3zukLoaded = type != null;

                if (IsWar3zukLoaded)
                {
                    ModLogger.Info("War3zuk AIO detectado. Aplicando modo de compatibilidad.");
                }
                else
                {
                    ModLogger.Info("War3zuk AIO no detectado. Operando en modo estándar.");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Warning("No se pudo comprobar la presencia de War3zuk AIO.");
                ModLogger.Debug(ex.ToString());
            }
        }
    }
}
