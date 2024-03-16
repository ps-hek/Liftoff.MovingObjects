using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Liftoff.MovingObjects.Player;
using Liftoff.MovingObjects.Utils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.UIElements;
using static Liftoff.MovingObjects.Shared.Editor;
using Button = UnityEngine.UIElements.Button;
using Logger = BepInEx.Logging.Logger;
using Toggle = UnityEngine.UIElements.Toggle;

namespace Liftoff.MovingObjects;

internal class PlacementUtilsWindow : MonoBehaviour
{
    private static readonly ManualLogSource Log =
        Logger.CreateLogSource($"{PluginInfo.PLUGIN_NAME}.{nameof(PlacementUtilsWindow)}");

    private VisualElement _root;
    private ItemInfo _selectedItem;

    private UIDocument _uiDocument;

    public Assets assets;

    private void Awake()
    {
        _uiDocument = gameObject.AddComponent<UIDocument>();
        _uiDocument.visualTreeAsset = assets.VisualTreeAsset;
        _uiDocument.panelSettings = assets.PanelSettings;
        _uiDocument.rootVisualElement.StretchToParentSize();

        Shared.Editor.OnItemSelected += OnItemSelected;
        Shared.Editor.OnItemCleared += OnItemCleared;
    }

    private void OnDestroy()
    {
        Shared.Editor.OnItemSelected -= OnItemSelected;
        Shared.Editor.OnItemCleared -= OnItemCleared;
    }

    private void OnItemCleared()
    {
        if (_selectedItem == null)
            return;

        foreach (var trackItemFlag in FindObjectsOfType<TrackItemFlag>())
        {
            var t = trackItemFlag.gameObject.transform;
            if (t.parent == _selectedItem.gameObject.transform)
                t.parent = null;
        }

        _selectedItem = null;
        DeselectAll();
    }

    private void OnItemSelected(ItemInfo selectedItem)
    {
        _selectedItem = selectedItem;

        DeselectAll();
        if (!Shared.PlacementUtils.EnchantedEditor || string.IsNullOrEmpty(selectedItem.blueprint.mo_groupId))
            return;

        foreach (var info in FindItemsByGroupId(selectedItem.blueprint.mo_groupId))
        {
            info.gameObject.transform.parent = selectedItem.gameObject.transform;

            var groupHighlightObj = Highlight(info.gameObject);
            if (groupHighlightObj != null)
                groupHighlightObj.AddComponent<GroupSelectionInfo>().trackBlueprint = selectedItem.blueprint;
        }
    }

    private void OnEnable()
    {
        // Dirty hack for add focus support
        GameObject.Find("UtilsWindowPanelSettings").AddComponent<InputField>().interactable = false;
        _root = _uiDocument.rootVisualElement;

        GuiUtils.SetVisible(_root, false);

        GuiUtils.ConvertToFloatField(_root.Q<TextField>("grid-align-value"),
            f => Shared.PlacementUtils.GridRound = f, Shared.PlacementUtils.GridRound);
        GuiUtils.ConvertToFloatField(_root.Q<TextField>("drag-grid-align-value"),
            f => Shared.PlacementUtils.DragGridRound = f, Shared.PlacementUtils.DragGridRound);
        _root.Q<Button>("grid-align").clicked += RoundGizmoLocation;

        _root.Q<Toggle>("enchanted-editor")
            .RegisterValueChangedCallback(evt => Shared.PlacementUtils.EnchantedEditor = evt.newValue);

        RefreshGui();
    }

    private void RefreshGui()
    {
        _root.Q<TextField>("grid-align-value").value = GuiUtils.FloatToString(Shared.PlacementUtils.GridRound);
        _root.Q<TextField>("drag-grid-align-value").value = GuiUtils.FloatToString(Shared.PlacementUtils.DragGridRound);
        _root.Q<Toggle>("enchanted-editor").value = Shared.PlacementUtils.EnchantedEditor;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
            RoundGizmoLocation();
        else if (_root != null && Input.GetKeyDown(KeyCode.F2))
            GuiUtils.ToggleVisible(_root);
        if (!Shared.PlacementUtils.EnchantedEditor)
            return;

        if (Input.GetKey(KeyCode.LeftControl))
            HandleEnchantedKeys();

        if (_selectedItem == null && Input.GetMouseButtonDown((int)MouseButton.MiddleMouse))
            HandleSelection();
    }

    private List<ItemInfo> FindItemsByGroupId(string groupId)
    {
        var items = new List<ItemInfo>();
        foreach (var trackItemFlag in FindObjectsOfType<TrackItemFlag>())
        {
            var info = ReflectionUtils.GetPrivateFieldValueByType<TrackBlueprint>(trackItemFlag);
            if (info != null && string.Equals(groupId, info.mo_groupId))
                items.Add(new ItemInfo { blueprint = info, gameObject = trackItemFlag.gameObject });
        }

        return items;
    }

    private void HandleEnchantedKeys()
    {
        if (!Input.GetKeyDown(KeyCode.G))
            return;
        if (_selectedItem == null)
        {
            var groupId = Guid.NewGuid().ToString("D");
            foreach (var info in FindObjectsOfType<GroupSelectionInfo>())
                info.trackBlueprint.mo_groupId = groupId;
        }
        else if (!string.IsNullOrEmpty(_selectedItem.blueprint.mo_groupId))
        {
            foreach (var itemInfo in FindItemsByGroupId(_selectedItem.blueprint.mo_groupId))
            {
                itemInfo.blueprint.mo_groupId = null;
                itemInfo.gameObject.transform.parent = null;
            }
        }

        DeselectAll();
    }

    private void DeselectAll()
    {
        foreach (var info in FindObjectsOfType<GroupSelectionInfo>())
            Destroy(info.gameObject);
    }

    private GameObject Highlight(GameObject targetObject)
    {
        var highlightObj = targetObject.transform.Find(targetObject.name + "(Clone)_Overlay");
        if (highlightObj == null)
            return null;

        var groupHighlightName = highlightObj.gameObject.name + "_Overlay_MO";

        var groupHighlightObj = Instantiate(highlightObj.gameObject, targetObject.transform);
        groupHighlightObj.name = groupHighlightName;
        groupHighlightObj.transform.localScale = highlightObj.localScale;
        groupHighlightObj.transform.localPosition = highlightObj.localPosition;
        groupHighlightObj.transform.localRotation = highlightObj.localRotation;

        var renderers = new List<Renderer>();
        var shaderOverride = groupHighlightObj.GetComponent<TrackEditorOverlayShaderOverride>();
        if (shaderOverride != null)
            renderers.AddRange(shaderOverride.affectedRenderers);
        renderers.AddRange(groupHighlightObj.GetComponentsInChildren<Renderer>());

        foreach (var t in renderers.SelectMany(renderer => renderer.materials))
            t.SetColor("_OverlayColor", Color.magenta);

        groupHighlightObj.SetActive(true);
        return groupHighlightObj;
    }


    private void HandleSelection()
    {
        var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (EventSystem.current.IsPointerOverGameObject())
            return;

        if (!Physics.Raycast(ray, out var raycastHit))
        {
            DeselectAll();
            return;
        }

        var trackItemFlag = raycastHit.transform.gameObject.GetComponentInParent<TrackItemFlag>();
        if (trackItemFlag == null)
        {
            DeselectAll();
            return;
        }

        var selectedObject = trackItemFlag.gameObject;
        var existsGroupInfo = selectedObject.GetComponentInChildren<GroupSelectionInfo>();
        if (existsGroupInfo != null)
        {
            Destroy(existsGroupInfo.gameObject);
            return;
        }

        var blueprint = ReflectionUtils.GetPrivateFieldValueByType<TrackBlueprint>(trackItemFlag);
        var groupHighlightObj = Highlight(selectedObject);
        if (groupHighlightObj == null)
            return;

        groupHighlightObj.AddComponent<GroupSelectionInfo>().trackBlueprint = blueprint;
    }


    private static void RoundGizmoLocation()
    {
        var gizmo = GameObject.Find("TrackEditorGizmo");
        if (gizmo == null)
            return;

        gizmo.transform.position =
            GirdUtils.RoundVectorToStep(gizmo.transform.position, Shared.PlacementUtils.GridRound);
    }

    private class GroupSelectionInfo : MonoBehaviour
    {
        public TrackBlueprint trackBlueprint;
    }

    public struct Assets
    {
        public VisualTreeAsset VisualTreeAsset { get; set; }
        public PanelSettings PanelSettings { get; set; }
    }
}