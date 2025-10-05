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
        private Harmony harmony;

        public void InitMod(Mod mod)
        {
            try
            {
                ModContext.Initialize(mod);

                ModLogger.Log("🎯 [ZombiesDeOP] Iniciando mod para Alpha 21...");

                harmony = new Harmony(HARMONY_ID);
                harmony.PatchAll(Assembly.GetExecutingAssembly());

                ModSettings.Load();
                War3zukCompatibility.ApplyCompatibility();

                DetectionSystem.Initialize();
                HUDManager.Initialize();
                BehaviorManager.Initialize();

                ModLogger.Log("✅ [ZombiesDeOP] Mod cargado exitosamente");
            }
            catch (Exception e)
            {
                ModLogger.Error($"❌ [ZombiesDeOP] Error en inicialización: {e}");
            }
        }

        public void Shutdown()
        {
            try
            {
                harmony?.UnpatchAll(HARMONY_ID);
                HUDManager.Shutdown();
                DetectionSystem.Shutdown();
                BehaviorManager.Shutdown();
                ModLogger.Log("✅ [ZombiesDeOP] Mod descargado correctamente");
            }
            catch (Exception e)
            {
                ModLogger.Error($"❌ [ZombiesDeOP] Error en shutdown: {e}");
            }
        }
    }
}
