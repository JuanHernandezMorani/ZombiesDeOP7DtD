using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;
using ZombiesDeOP.Compatibility;
using ZombiesDeOP.Systems;
using ZombiesDeOP.Utilities;

namespace ZombiesDeOP
{
    public class ZombiesDeOPMain : IModApi
    {
        private const string HARMONY_ID = "com.juanhernandez.zombiesdeop.a21";
        private static HarmonyLib.Harmony harmonyInstance;
        private static bool overlayInitialized;

        public void InitMod(Mod mod)
        {
            try
            {
                ModContext.Initialize(mod);

                ModLogger.Info("🎯 [ZombiesDeOP] Iniciando mod para Alpha 21...");

                if (harmonyInstance == null)
                {
                    harmonyInstance = new HarmonyLib.Harmony(HARMONY_ID);
                    harmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
                }

                ModSettings.Load();
                War3zukCompatibility.ApplyCompatibility();

                DetectionSystem.Initialize();
                HUDManager.Initialize();
                InitializeOverlay();
                BehaviorManager.Initialize();

                ModLogger.Info("✅ [ZombiesDeOP] Mod cargado exitosamente");
            }
            catch (Exception e)
            {
                ModLogger.Error("❌ [ZombiesDeOP] Error en inicialización", e);
            }
        }

        public void Shutdown()
        {
            try
            {
                harmonyInstance?.UnpatchSelf();
                ModLogger.Info("[ZombiesDeOP] Harmony patches desactivados correctamente.");
                harmonyInstance = null;
                HUDManager.Shutdown();
                DetectionSystem.Shutdown();
                ShutdownOverlay();
                BehaviorManager.Shutdown();
                ModLogger.Info("✅ [ZombiesDeOP] Mod descargado correctamente");
            }
            catch (Exception e)
            {
                ModLogger.Error("❌ [ZombiesDeOP] Error en shutdown", e);
            }
        }

        private static void InitializeOverlay()
        {
            if (overlayInitialized)
            {
                return;
            }

            try
            {
                VisibilityOverlaySystem.Initialize();
                overlayInitialized = true;
            }
            catch (Exception e)
            {
                ModLogger.Error("❌ [ZombiesDeOP] Error al inicializar overlay", e);
            }
        }

        private static void ShutdownOverlay()
        {
            if (!overlayInitialized)
            {
                return;
            }

            try
            {
                VisibilityOverlaySystem.Shutdown();
            }
            catch (Exception e)
            {
                ModLogger.Error("❌ [ZombiesDeOP] Error al detener overlay", e);
            }
            finally
            {
                overlayInitialized = false;
            }
        }
    }
}
