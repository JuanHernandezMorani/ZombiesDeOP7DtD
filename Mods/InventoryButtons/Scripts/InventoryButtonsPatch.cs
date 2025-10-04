using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using HarmonyLib;
using UnityEngine;

namespace InventoryButtons
{
    /// <summary>
    /// Punto de entrada principal del módulo: sincroniza la configuración y parchea la UI de inventario/loot.
    /// </summary>
    [HarmonyPatch]
    public static class InventoryButtonsPatch
    {
        private static bool s_bootstrapped;

        [HarmonyPatch(typeof(GameManager), "Awake")]
        [HarmonyPostfix]
        private static void OnGameManagerAwake()
        {
            if (s_bootstrapped)
            {
                return;
            }

            InventoryButtonsConfig.EnsureLoaded();
            s_bootstrapped = true;
        }

        [HarmonyPatch(typeof(GameManager), "Update")]
        [HarmonyPostfix]
        private static void OnGameManagerUpdate()
        {
            if (!s_bootstrapped)
            {
                InventoryButtonsConfig.EnsureLoaded();
                s_bootstrapped = true;
            }

            InventoryButtonsConfig.Update();
            InventoryButtonsManager.Tick();
        }

        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetWindowInitialisation()
        {
            string[] candidateTypes =
            {
                "XUiC_WindowGroup",
                "XUiWindowGroup"
            };

            foreach (string typeName in candidateTypes)
            {
                var type = AccessTools.TypeByName(typeName);
                if (type == null)
                {
                    continue;
                }

                var method = AccessTools.Method(type, "Init");
                if (method != null)
                {
                    yield return method;
                }
            }
        }

        [HarmonyPostfix]
        private static void OnWindowGroupInit(object __instance)
        {
            InventoryButtonsManager.TryInjectButtons(__instance);
        }
    }

    /// <summary>
    /// Gestor principal responsable de detectar ventanas válidas y añadir los botones configurables.
    /// </summary>
    internal static class InventoryButtonsManager
    {
        private static readonly HashSet<int> InjectedWindows = new();
        private static readonly string[] InventoryWindowIds =
        {
            "windowbackpack",
            "windowbag",
            "windowplayerinventory",
            "war3zukwindowbackpack"
        };

        private static readonly string[] LootWindowIds =
        {
            "windowlooting",
            "windowloot",
            "war3zukwindowlooting"
        };

        internal static void Tick()
        {
            // Reservado para lógica futura (por ejemplo, refrescos diferidos).
        }

        internal static void TryInjectButtons(object windowGroup)
        {
            if (windowGroup == null)
            {
                return;
            }

            string identifier = ResolveWindowIdentifier(windowGroup);
            if (string.IsNullOrEmpty(identifier))
            {
                return;
            }

            bool isInventory = InventoryWindowIds.Any(id => string.Equals(id, identifier, StringComparison.OrdinalIgnoreCase));
            bool isLoot = LootWindowIds.Any(id => string.Equals(id, identifier, StringComparison.OrdinalIgnoreCase));

            if (!isInventory && !isLoot)
            {
                return;
            }

            if (!InventoryButtonsConfig.SortEnabled && !InventoryButtonsConfig.LootAllEnabled)
            {
                return;
            }

            int hash = RuntimeHelpers.GetHashCode(windowGroup);
            if (!InjectedWindows.Add(hash))
            {
                return;
            }

            try
            {
                InjectButtonSet(windowGroup, isInventory, isLoot);
                InventoryButtonsLogger.Info($"Botones inyectados en ventana '{identifier}'.");
            }
            catch (Exception exception)
            {
                InventoryButtonsLogger.Warn($"Error al inyectar botones en '{identifier}': {exception.Message}");
            }
        }

        private static void InjectButtonSet(object windowGroup, bool isInventory, bool isLoot)
        {
            var templateButton = FindTemplateButton(windowGroup);
            if (templateButton == null)
            {
                InventoryButtonsLogger.Warn("No se encontró un botón base para clonar dentro de la ventana destino.");
                return;
            }

            var templateTransform = GetControllerTransform(templateButton);
            if (templateTransform == null)
            {
                InventoryButtonsLogger.Warn("El botón base carece de Transform válido, no se puede continuar.");
                return;
            }

            Transform parentTransform = templateTransform.parent;
            if (parentTransform == null)
            {
                InventoryButtonsLogger.Warn("No se pudo resolver el contenedor de botones en la ventana objetivo.");
                return;
            }

            int insertionIndex = templateTransform.GetSiblingIndex() + 1;

            if (isInventory && InventoryButtonsConfig.SortEnabled)
            {
                CreateButtonFromTemplate(windowGroup, templateButton, parentTransform, insertionIndex++, "btnInventoryButtonsSort", "Sort", nameof(OnSortButtonPressed));
            }

            if (isLoot && InventoryButtonsConfig.LootAllEnabled)
            {
                CreateButtonFromTemplate(windowGroup, templateButton, parentTransform, insertionIndex, "btnInventoryButtonsLootAll", "Loot All", nameof(OnLootAllButtonPressed));
            }

            TryRefreshWindow(windowGroup);
        }

        private static object FindTemplateButton(object windowGroup)
        {
            // Prioridad: botones conocidos para asegurar coherencia visual.
            string[] preferredIds =
            {
                "btnTakeAll",
                "btnStack",
                "btnSort",
                "btnLootAll"
            };

            foreach (string id in preferredIds)
            {
                var candidate = FindChildController(windowGroup, id);
                if (candidate != null)
                {
                    return candidate;
                }
            }

            // Como fallback, seleccionamos el primer controlador que contenga "Button" en su nombre de tipo.
            foreach (var child in EnumerateControllers(windowGroup))
            {
                if (child == null)
                {
                    continue;
                }

                string typeName = child.GetType().Name;
                if (typeName.IndexOf("button", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return child;
                }
            }

            return null;
        }

        private static void CreateButtonFromTemplate(object windowGroup, object templateButton, Transform parent, int siblingIndex, string newId, string label, string callbackName)
        {
            Transform templateTransform = GetControllerTransform(templateButton);
            if (templateTransform == null)
            {
                throw new InvalidOperationException("El botón base no dispone de Transform válido.");
            }

            GameObject cloneObject = UnityEngine.Object.Instantiate(templateTransform.gameObject, parent);
            cloneObject.name = newId;
            Transform cloneTransform = cloneObject.transform;
            cloneTransform.SetSiblingIndex(Mathf.Clamp(siblingIndex, 0, parent.childCount - 1));

            object cloneController = cloneObject.GetComponent(templateButton.GetType());
            if (cloneController == null)
            {
                throw new InvalidOperationException("El clon no contiene el componente controlador esperado.");
            }

            SetControllerId(cloneController, newId);
            ResetButtonCallbacks(cloneController);
            SetButtonLabel(cloneController, label);
            RegisterButtonCallback(cloneController, callbackName);
            TryAlignScaleAndPosition(templateTransform, cloneTransform);
            PropagateWindowGroup(cloneController, windowGroup);
        }

        private static void PropagateWindowGroup(object controller, object windowGroup)
        {
            var type = controller.GetType();
            var parentField = AccessTools.Field(type, "parent") ?? AccessTools.Field(type, "Parent");
            parentField?.SetValue(controller, windowGroup);
        }

        private static void TryAlignScaleAndPosition(Transform template, Transform clone)
        {
            clone.localScale = template.localScale;
            clone.localPosition = template.localPosition;
            clone.localRotation = template.localRotation;
        }

        private static void RegisterButtonCallback(object controller, string methodName)
        {
            if (controller == null)
            {
                return;
            }

            var type = controller.GetType();
            var eventInfo = type.GetEvent("OnPress");
            if (eventInfo == null)
            {
                InventoryButtonsLogger.Warn($"El controlador {type.Name} no expone el evento OnPress.");
                return;
            }

            var handler = Delegate.CreateDelegate(eventInfo.EventHandlerType, typeof(InventoryButtonsManager).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static));
            eventInfo.AddEventHandler(controller, handler);
        }

        private static void ResetButtonCallbacks(object controller)
        {
            if (controller == null)
            {
                return;
            }

            var type = controller.GetType();
            var eventField = AccessTools.Field(type, "OnPress");
            if (eventField != null && typeof(Delegate).IsAssignableFrom(eventField.FieldType))
            {
                eventField.SetValue(controller, null);
            }
        }

        private static void SetButtonLabel(object controller, string text)
        {
            if (controller == null)
            {
                return;
            }

            var type = controller.GetType();
            var method = AccessTools.Method(type, "SetText", new[] { typeof(string) }) ?? AccessTools.Method(type, "SetLabel", new[] { typeof(string) });
            if (method != null)
            {
                method.Invoke(controller, new object[] { text });
                return;
            }

            var property = AccessTools.Property(type, "Text") ?? AccessTools.Property(type, "Label") ?? AccessTools.Property(type, "LabelText");
            if (property != null)
            {
                if (property.CanWrite)
                {
                    property.SetValue(controller, text);
                    return;
                }

                var nested = property.GetValue(controller, null);
                if (nested != null)
                {
                    SetLabelOnView(nested, text);
                    return;
                }
            }

            SetLabelOnView(controller, text);
        }

        private static void SetLabelOnView(object target, string text)
        {
            if (target == null)
            {
                return;
            }

            if (target is Component component)
            {
                TryAssignUILabel(component.gameObject, text);
                return;
            }

            var type = target.GetType();
            var viewProperty = AccessTools.Property(type, "ViewComponent") ?? AccessTools.Property(type, "viewComponent");
            if (viewProperty != null)
            {
                var view = viewProperty.GetValue(target, null);
                if (view is Component viewComponent)
                {
                    TryAssignUILabel(viewComponent.gameObject, text);
                }
            }
        }

        private static void TryAssignUILabel(GameObject target, string text)
        {
            if (target == null)
            {
                return;
            }

            var uiLabelType = AccessTools.TypeByName("UILabel") ?? AccessTools.TypeByName("XUiV_Label");
            if (uiLabelType == null)
            {
                return;
            }

            var labelComponent = target.GetComponent(uiLabelType);
            if (labelComponent == null)
            {
                return;
            }

            var setTextMethod = AccessTools.Method(uiLabelType, "SetText", new[] { typeof(string) }) ?? AccessTools.Method(uiLabelType, "SetValue", new[] { typeof(string) });
            if (setTextMethod != null)
            {
                setTextMethod.Invoke(labelComponent, new object[] { text });
                return;
            }

            var textField = AccessTools.Field(uiLabelType, "text");
            textField?.SetValue(labelComponent, text);
        }

        private static void SetControllerId(object controller, string id)
        {
            var type = controller.GetType();
            var property = AccessTools.Property(type, "Id") ?? AccessTools.Property(type, "ID") ?? AccessTools.Property(type, "ControllerId");
            if (property != null && property.CanWrite)
            {
                property.SetValue(controller, id);
                return;
            }

            var field = AccessTools.Field(type, "id") ?? AccessTools.Field(type, "ID") ?? AccessTools.Field(type, "controllerId");
            field?.SetValue(controller, id);
        }

        private static void TryRefreshWindow(object windowGroup)
        {
            var type = windowGroup.GetType();
            var refreshMethod = AccessTools.Method(type, "RefreshBindings", new[] { typeof(bool) }) ?? AccessTools.Method(type, "RefreshBindings") ?? AccessTools.Method(type, "RefreshAllBindings");
            if (refreshMethod != null)
            {
                try
                {
                    object[] parameters = refreshMethod.GetParameters().Length == 1 ? new object[] { true } : Array.Empty<object>();
                    refreshMethod.Invoke(windowGroup, parameters);
                }
                catch (Exception exception)
                {
                    InventoryButtonsLogger.Debug($"RefreshBindings falló: {exception.Message}");
                }
            }
        }

        private static string ResolveWindowIdentifier(object windowGroup)
        {
            var type = windowGroup.GetType();
            string id = TryReadString(type, windowGroup, "WindowGroupID") ??
                        TryReadString(type, windowGroup, "windowGroupId") ??
                        TryReadString(type, windowGroup, "WindowID") ??
                        TryReadString(type, windowGroup, "ID");

            if (!string.IsNullOrEmpty(id))
            {
                return id;
            }

            var nameField = AccessTools.Field(type, "windowName") ?? AccessTools.Field(type, "name");
            if (nameField?.GetValue(windowGroup) is string fieldName && !string.IsNullOrEmpty(fieldName))
            {
                return fieldName;
            }

            return type.Name;
        }

        private static string TryReadString(Type type, object instance, string memberName)
        {
            var property = AccessTools.Property(type, memberName);
            if (property != null)
            {
                var value = property.GetValue(instance, null) as string;
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }

            var field = AccessTools.Field(type, memberName);
            if (field != null)
            {
                var value = field.GetValue(instance) as string;
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }

            return null;
        }

        private static IEnumerable<object> EnumerateControllers(object controller)
        {
            if (controller == null)
            {
                yield break;
            }

            var type = controller.GetType();
            var property = AccessTools.Property(type, "Controllers") ?? AccessTools.Property(type, "ChildControllers");
            if (property != null)
            {
                if (property.GetValue(controller, null) is IEnumerable enumerable)
                {
                    foreach (var item in enumerable)
                    {
                        if (item != null)
                        {
                            yield return item;
                        }
                    }
                }
            }

            var field = AccessTools.Field(type, "controllers") ?? AccessTools.Field(type, "childControllers") ?? AccessTools.Field(type, "mControllers");
            if (field != null)
            {
                if (field.GetValue(controller) is IEnumerable enumerable)
                {
                    foreach (var item in enumerable)
                    {
                        if (item != null)
                        {
                            yield return item;
                        }
                    }
                }
            }
        }

        private static object FindChildController(object root, string id)
        {
            foreach (var child in EnumerateControllers(root))
            {
                string childId = GetControllerId(child);
                if (!string.IsNullOrEmpty(childId) && string.Equals(childId, id, StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }

                var nested = FindChildController(child, id);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static string GetControllerId(object controller)
        {
            if (controller == null)
            {
                return null;
            }

            var type = controller.GetType();
            var property = AccessTools.Property(type, "Id") ?? AccessTools.Property(type, "ID") ?? AccessTools.Property(type, "ControllerId");
            if (property != null)
            {
                return property.GetValue(controller, null) as string;
            }

            var field = AccessTools.Field(type, "id") ?? AccessTools.Field(type, "ID") ?? AccessTools.Field(type, "controllerId");
            if (field != null)
            {
                return field.GetValue(controller) as string;
            }

            return null;
        }

        private static Transform GetControllerTransform(object controller)
        {
            if (controller == null)
            {
                return null;
            }

            if (controller is Component component)
            {
                return component.transform;
            }

            var type = controller.GetType();
            var property = AccessTools.Property(type, "UiTransform") ?? AccessTools.Property(type, "UITransform") ?? AccessTools.Property(type, "Transform");
            if (property != null)
            {
                if (property.GetValue(controller, null) is Transform transform)
                {
                    return transform;
                }

                if (property.GetValue(controller, null) is Component viewComponent)
                {
                    return viewComponent.transform;
                }
            }

            var viewProperty = AccessTools.Property(type, "ViewComponent") ?? AccessTools.Property(type, "viewComponent");
            if (viewProperty != null)
            {
                if (viewProperty.GetValue(controller, null) is Component viewComponent)
                {
                    return viewComponent.transform;
                }
            }

            var field = AccessTools.Field(type, "viewComponent");
            if (field != null)
            {
                if (field.GetValue(controller) is Component fieldComponent)
                {
                    return fieldComponent.transform;
                }
            }

            return null;
        }

        private static void OnSortButtonPressed(object controller, int mouseButton, bool isDown)
        {
            if (!isDown || !InventoryButtonsConfig.SortEnabled)
            {
                return;
            }

            try
            {
                ExecuteSort(controller);
            }
            catch (Exception exception)
            {
                InventoryButtonsLogger.Warn($"Fallo al ordenar inventario: {exception.Message}");
            }
        }

        private static void OnLootAllButtonPressed(object controller, int mouseButton, bool isDown)
        {
            if (!isDown || !InventoryButtonsConfig.LootAllEnabled)
            {
                return;
            }

            try
            {
                ExecuteLootAll(controller);
            }
            catch (Exception exception)
            {
                InventoryButtonsLogger.Warn($"Fallo al saquear contenedor: {exception.Message}");
            }
        }

        private static void ExecuteSort(object controller)
        {
            var playerUI = ResolveLocalPlayerUI(controller);
            if (playerUI == null)
            {
                InventoryButtonsLogger.Warn("No se pudo localizar LocalPlayerUI para ordenar el inventario.");
                return;
            }

            var xui = ResolveXUi(playerUI);
            if (xui == null)
            {
                InventoryButtonsLogger.Warn("No se pudo resolver la instancia de XUi desde LocalPlayerUI.");
                return;
            }

            object bagWindow = TryResolveMember(xui, new[] { "bagWindow", "backpackWindow", "playerInventoryWindow" });
            if (bagWindow == null)
            {
                InventoryButtonsLogger.Warn("No se encontró la ventana de inventario en XUi.");
                return;
            }

            if (InvokeMethodAny(bagWindow, new[] { "OnSortClicked", "OnSortPressed", "SortInventory", "SortItems", "Sort" }))
            {
                InventoryButtonsLogger.Debug("Ordenación delegada al controlador de inventario.");
                return;
            }

            object grid = TryResolveMember(bagWindow, new[] { "itemGrid", "grid" });
            if (grid != null && InvokeMethodAny(grid, new[] { "Sort", "SortItems" }))
            {
                InventoryButtonsLogger.Debug("Ordenación ejecutada a través de ItemStackGrid.");
                return;
            }

            InventoryButtonsLogger.Warn("No se encontraron métodos de ordenación disponibles en la ventana de inventario.");
        }

        private static void ExecuteLootAll(object controller)
        {
            var playerUI = ResolveLocalPlayerUI(controller);
            if (playerUI == null)
            {
                InventoryButtonsLogger.Warn("No se pudo localizar LocalPlayerUI para saquear.");
                return;
            }

            var xui = ResolveXUi(playerUI);
            if (xui == null)
            {
                InventoryButtonsLogger.Warn("No se pudo resolver la instancia de XUi desde LocalPlayerUI.");
                return;
            }

            object lootWindow = TryResolveMember(xui, new[] { "lootWindow", "lootingWindow", "containerWindow" });
            if (lootWindow == null)
            {
                InventoryButtonsLogger.Warn("No se encontró la ventana de saqueo activa.");
                return;
            }

            if (InvokeMethodAny(lootWindow, new[] { "OnTakeAllPressed", "TakeAll", "TakeAllItems", "LootAll" }))
            {
                InventoryButtonsLogger.Debug("Saqueo completo ejecutado mediante la ventana de loot.");
                return;
            }

            object controllerField = TryResolveMember(lootWindow, new[] { "itemStackController", "itemGrid" });
            if (controllerField != null && InvokeMethodAny(controllerField, new[] { "TakeAll", "MoveAllToInventory" }))
            {
                InventoryButtonsLogger.Debug("Saqueo completo ejecutado mediante el controlador de grid.");
                return;
            }

            InventoryButtonsLogger.Warn("No se encontraron métodos TakeAll compatibles en la ventana de loot.");
        }

        private static bool InvokeMethodAny(object instance, IEnumerable<string> methodNames)
        {
            if (instance == null)
            {
                return false;
            }

            var type = instance.GetType();
            foreach (string methodName in methodNames)
            {
                var method = AccessTools.Method(type, methodName);
                if (method == null)
                {
                    continue;
                }

                var parameters = method.GetParameters();
                object[] args = parameters.Length == 0 ? Array.Empty<object>() : new object[parameters.Length];
                try
                {
                    method.Invoke(instance, args);
                    return true;
                }
                catch (Exception exception)
                {
                    InventoryButtonsLogger.Debug($"Invocation {methodName} fallida: {exception.Message}");
                }
            }

            return false;
        }

        private static object ResolveLocalPlayerUI(object controller)
        {
            if (controller == null)
            {
                return null;
            }

            var type = controller.GetType();
            var xuiField = AccessTools.Field(type, "xui") ?? AccessTools.Property(type, "xui");
            object xui = xuiField switch
            {
                FieldInfo fieldInfo => fieldInfo.GetValue(controller),
                PropertyInfo propertyInfo => propertyInfo.GetValue(controller, null),
                _ => null
            };

            if (xui == null)
            {
                var parentField = AccessTools.Field(type, "parent") ?? AccessTools.Field(type, "Parent");
                var parent = parentField?.GetValue(controller);
                if (parent != null && !ReferenceEquals(parent, controller))
                {
                    return ResolveLocalPlayerUI(parent);
                }

                return null;
            }

            var playerUiField = AccessTools.Field(xui.GetType(), "playerUI") ?? AccessTools.Property(xui.GetType(), "playerUI") ?? AccessTools.Property(xui.GetType(), "PlayerUI");
            return playerUiField switch
            {
                FieldInfo fieldInfo => fieldInfo.GetValue(xui),
                PropertyInfo propertyInfo => propertyInfo.GetValue(xui, null),
                _ => null
            };
        }

        private static object ResolveXUi(object playerUi)
        {
            if (playerUi == null)
            {
                return null;
            }

            var type = playerUi.GetType();
            var property = AccessTools.Property(type, "xui") ?? AccessTools.Property(type, "Xui");
            if (property != null)
            {
                var value = property.GetValue(playerUi, null);
                if (value != null)
                {
                    return value;
                }
            }

            var field = AccessTools.Field(type, "xui");
            return field?.GetValue(playerUi);
        }

        private static object TryResolveMember(object instance, IEnumerable<string> candidateNames)
        {
            if (instance == null)
            {
                return null;
            }

            var type = instance.GetType();
            foreach (string name in candidateNames)
            {
                var property = AccessTools.Property(type, name);
                if (property != null)
                {
                    var value = property.GetValue(instance, null);
                    if (value != null)
                    {
                        return value;
                    }
                }

                var field = AccessTools.Field(type, name);
                if (field != null)
                {
                    var value = field.GetValue(instance);
                    if (value != null)
                    {
                        return value;
                    }
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Gestor de configuración: lee InventoryButtons.xml y notifica cambios en caliente.
    /// </summary>
    internal static class InventoryButtonsConfig
    {
        private const string ConfigFileName = "InventoryButtons.xml";

        private static readonly object Sync = new();
        private static FileSystemWatcher s_watcher;
        private static bool s_loaded;
        private static bool s_pendingReload;
        private static DateTime s_reloadRequestedAt;

        internal static bool SortEnabled { get; private set; } = true;
        internal static bool LootAllEnabled { get; private set; } = true;

        internal static void EnsureLoaded()
        {
            lock (Sync)
            {
                if (s_loaded)
                {
                    return;
                }

                Load();
                SetupWatcher();
                s_loaded = true;
            }
        }

        internal static void Update()
        {
            if (!s_pendingReload)
            {
                return;
            }

            if ((DateTime.UtcNow - s_reloadRequestedAt).TotalMilliseconds < 250)
            {
                return;
            }

            lock (Sync)
            {
                s_pendingReload = false;
                Load();
            }
        }

        private static void SetupWatcher()
        {
            try
            {
                string path = GetConfigPath();
                string directory = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(directory))
                {
                    return;
                }

                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                s_watcher = new FileSystemWatcher(directory, ConfigFileName)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true
                };

                s_watcher.Changed += OnConfigChanged;
                s_watcher.Created += OnConfigChanged;
                s_watcher.Renamed += OnConfigChanged;
            }
            catch (Exception exception)
            {
                InventoryButtonsLogger.Warn($"No se pudo vigilar InventoryButtons.xml: {exception.Message}");
            }
        }

        private static void OnConfigChanged(object sender, FileSystemEventArgs e)
        {
            lock (Sync)
            {
                s_pendingReload = true;
                s_reloadRequestedAt = DateTime.UtcNow;
            }
        }

        private static void Load()
        {
            string path = GetConfigPath();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);

                if (!File.Exists(path))
                {
                    CreateDefaultConfig(path);
                    SortEnabled = true;
                    LootAllEnabled = true;
                    InventoryButtonsLogger.Info($"Archivo InventoryButtons.xml creado en {path}.");
                    return;
                }

                var document = XDocument.Load(path);
                var root = document.Root;
                if (root == null || !string.Equals(root.Name.LocalName, "InventoryButtons", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("InventoryButtons.xml carece de nodo raíz válido.");
                }

                SortEnabled = ReadButtonState(root, "Sort", true);
                LootAllEnabled = ReadButtonState(root, "LootAll", true);

                InventoryButtonsLogger.Info($"Configuración cargada. Sort={SortEnabled}, LootAll={LootAllEnabled}.");
            }
            catch (Exception exception)
            {
                SortEnabled = true;
                LootAllEnabled = true;
                InventoryButtonsLogger.Warn($"No se pudo leer InventoryButtons.xml: {exception.Message}. Se usan valores por defecto.");
            }
        }

        private static bool ReadButtonState(XElement root, string name, bool defaultValue)
        {
            foreach (var element in root.Elements("Button"))
            {
                var nameAttribute = element.Attribute("name");
                if (nameAttribute == null)
                {
                    continue;
                }

                if (!string.Equals(nameAttribute.Value, name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var enabledAttribute = element.Attribute("enabled") ?? element.Attribute("value");
                if (enabledAttribute == null)
                {
                    return defaultValue;
                }

                if (bool.TryParse(enabledAttribute.Value, out bool parsed))
                {
                    return parsed;
                }

                if (int.TryParse(enabledAttribute.Value, out int numeric))
                {
                    return numeric != 0;
                }
            }

            return defaultValue;
        }

        private static void CreateDefaultConfig(string path)
        {
            var document = new XDocument(
                new XElement("InventoryButtons",
                    new XElement("Button", new XAttribute("name", "Sort"), new XAttribute("enabled", "true")),
                    new XElement("Button", new XAttribute("name", "LootAll"), new XAttribute("enabled", "true"))));

            document.Save(path);
        }

        private static string GetConfigPath()
        {
            var assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var basePath = string.IsNullOrEmpty(assemblyPath)
                ? Environment.CurrentDirectory
                : Path.GetFullPath(Path.Combine(assemblyPath, ".."));

            return Path.Combine(basePath, "Config", ConfigFileName);
        }
    }

    /// <summary>
    /// Logger específico del módulo para facilitar depuración.
    /// </summary>
    internal static class InventoryButtonsLogger
    {
        private const string Prefix = "[InventoryButtons] ";

        internal static void Info(string message)
        {
            Debug.Log(Prefix + message);
        }

        internal static void Warn(string message)
        {
            Debug.LogWarning(Prefix + message);
        }

        internal static void Debug(string message)
        {
#if DEBUG
            UnityEngine.Debug.Log(Prefix + "[DEBUG] " + message);
#else
            _ = message;
#endif
        }
    }
}
