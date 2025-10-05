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
        private static GameObject runtimeHost;
        private static UIOverlayComponent overlayComponent;

        public void InitMod(Mod mod)
        {
            try
            {
                TryInitializeContext(mod);

                ModLogger.Info("🎯 [ZombiesDeOP] Iniciando mod v2.4...");

                ModSettings.Load();
                ApplyCompatibilitySafe();
                InitializeHarmonySafe();
                CreateRuntimeSystems();
                InitializeGameplaySystems();

                ModLogger.Info("✅ [ZombiesDeOP] Mod inicializado completamente");
            }
            catch (Exception e)
            {
                ModLogger.Error("❌ [ZombiesDeOP] Error crítico en inicialización", e);
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
                ShutdownRuntimeHost();
                ModLogger.Info("✅ [ZombiesDeOP] Mod descargado correctamente");
            }
            catch (Exception e)
            {
                ModLogger.Error("❌ [ZombiesDeOP] Error en shutdown", e);
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
                overlayComponent = null;
            }
        }

        private static void TryInitializeContext(Mod mod)
        {
            try
            {
                ModContext.Initialize(mod);
            }
            catch (Exception e)
            {
                ModLogger.Warn($"⚠️ [ZombiesDeOP] No se pudo inicializar el contexto del mod: {e.Message}");
            }
        }

        private static void ApplyCompatibilitySafe()
        {
            try
            {
                War3zukCompatibility.ApplyCompatibility();
            }
            catch (Exception e)
            {
                ModLogger.Warn($"⚠️ [ZombiesDeOP] Error aplicando compatibilidad War3zuk: {e.Message}");
            }
        }

        private static void InitializeHarmonySafe()
        {
            if (!ModSettings.EnableHarmonyPatch)
            {
                ModLogger.Warn("⚠️ [ZombiesDeOP] Harmony deshabilitado por configuración; continuando con modo polling");
                return;
            }

            if (harmonyInstance != null)
            {
                return;
            }

            try
            {
                harmonyInstance = new HarmonyLib.Harmony(HARMONY_ID);
                harmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
                ModLogger.Info("✅ [ZombiesDeOP] Harmony patch aplicado correctamente");
            }
            catch (Exception e)
            {
                harmonyInstance = null;
                ModLogger.Warn($"⚠️ [ZombiesDeOP] Error en Harmony: {e.Message} - Continuando con polling system");
            }
        }

        private static void CreateRuntimeSystems()
        {
            try
            {
                if (runtimeHost == null)
                {
                    runtimeHost = new GameObject("ZombiesDeOP_Runtime");
                    runtimeHost.hideFlags = HideFlags.HideAndDontSave;
                    GameObject.DontDestroyOnLoad(runtimeHost);
                }

                overlayComponent = runtimeHost.GetComponent<UIOverlayComponent>() ?? runtimeHost.AddComponent<UIOverlayComponent>();
                VisibilityOverlaySystem.Initialize(overlayComponent);
                overlayComponent = VisibilityOverlaySystem.OverlayComponent ?? overlayComponent;
                overlayInitialized = VisibilityOverlaySystem.OverlayComponent != null;

                if (runtimeHost.GetComponent<DetectionSystemRuntime>() == null)
                {
                    runtimeHost.AddComponent<DetectionSystemRuntime>();
                }

                ModLogger.Info("✅ [ZombiesDeOP] Sistemas runtime creados correctamente");
            }
            catch (Exception e)
            {
                ModLogger.Error("❌ [ZombiesDeOP] Error creando sistemas runtime", e);
            }
        }

        private static void InitializeGameplaySystems()
        {
            ExecuteSafely(DetectionSystem.Initialize, "sistema de detección");
            ExecuteSafely(HUDManager.Initialize, "HUD");
            ExecuteSafely(BehaviorManager.Initialize, "sistema de comportamiento");
        }

        private static void ExecuteSafely(Action action, string description)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                ModLogger.Error($"❌ [ZombiesDeOP] Error inicializando {description}", e);
            }
        }

        private static void ShutdownRuntimeHost()
        {
            if (runtimeHost == null)
            {
                return;
            }

            try
            {
                UnityEngine.Object.Destroy(runtimeHost);
            }
            catch (Exception ex)
            {
                ModLogger.Error("⚠️ [ZombiesDeOP] Error al destruir runtime host", ex);
            }
            finally
            {
                runtimeHost = null;
            }
        }
    }
}
