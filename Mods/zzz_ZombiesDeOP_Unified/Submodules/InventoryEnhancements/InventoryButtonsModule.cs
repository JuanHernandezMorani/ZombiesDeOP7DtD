using ZombiesDeOPUnified.Core;

namespace ZombiesDeOPUnified.Submodules.InventoryEnhancements
{
    public sealed class InventoryButtonsModule : UnityEngine.MonoBehaviour, IModule
    {
        public bool IsEnabled => InventoryEnhancementConfig.EnableInventoryButtons;
        public string ModuleName => "Botones de Inventario";

        public void InitializeModule()
        {
            enabled = IsEnabled;
            if (IsEnabled)
            {
                ModLogger.Log("Módulo de botones de inventario habilitado");
            }
        }

        public void Shutdown()
        {
            enabled = false;
        }
    }
}
