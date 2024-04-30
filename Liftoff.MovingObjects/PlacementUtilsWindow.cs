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

    private IDisposable _fakeGroupContext;

    private VisualElement _root => _uiDocument.rootVisualElement;
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

        InvokeRepeating("UpdateStats", 1f, 1f);
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

        _fakeGroupContext?.Dispose();

        _selectedItem = null;
        DeselectAll();
    }

    private void OnItemSelected(ItemInfo selectedItem)
    {
        _selectedItem = selectedItem;

        DeselectAll();
        if (!Shared.PlacementUtils.EnchantedEditor || string.IsNullOrEmpty(selectedItem.blueprint.mo_groupId))
            return;
        _fakeGroupContext?.Dispose();

        var childs = FindItemsByGroupId(selectedItem.blueprint.mo_groupId)
            .Select(info => info.gameObject).Where(obj => obj != selectedItem.gameObject).ToList();
        _fakeGroupContext = FakeGroup.GroupObjects(selectedItem.gameObject, childs, false);

        foreach (var child in childs)
        {
            var groupHighlightObj = Highlight(child);
            if (groupHighlightObj != null)
                groupHighlightObj.AddComponent<GroupSelectionInfo>().trackBlueprint = selectedItem.blueprint;
        }
    }

    private void UpdateStats()
    {
        if (_root == null)
            return;

        var objects = EditorUtils.FindAllFlags().Where(c => c.gameObject.transform.parent?.name?.EndsWith("_DragParent") != true).ToList();
        _root.Q<Label>("object-count").text = objects.Count.ToString();
        _root.Q<Label>("triangle-count").text = objects.SelectMany(c => c.gameObject.GetComponentsInChildren<MeshFilter>()).Select(f => f.sharedMesh.triangles.Length / 3).Sum().ToString();
    }

    private void OnEnable()
    {
        // Dirty hack for add focus support
        GameObject.Find("UtilsWindowPanelSettings").AddComponent<InputField>().interactable = false;

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
        else if (Input.GetKeyDown(KeyCode.F3))
            ToggleNoClip();
        else if (Input.GetKeyDown(KeyCode.F4))
            ToggleWireframe();
        else if (_root != null && Input.GetKeyDown(KeyCode.F2))
            GuiUtils.ToggleVisible(_root);
        if (!Shared.PlacementUtils.EnchantedEditor)
            return;

        if (Input.GetKey(KeyCode.LeftControl))
            HandleEnchantedKeys();

        if (_selectedItem == null && Input.GetMouseButtonDown((int)MouseButton.MiddleMouse)) 
            HandleSelection();
    }

    private void ToggleWireframe()
    {
        var playerCamera = GameObject.Find("PlayerCamera")?.GetComponent<Camera>();
        if (playerCamera == null)
            return;

        var wireframe = playerCamera.gameObject.GetComponent<WireframeCamera>();
        if (wireframe == null)
        {
            wireframe = playerCamera.gameObject.AddComponent<WireframeCamera>();
            wireframe.enabled = false;
            wireframe.orignalClearFlags = playerCamera.clearFlags;
        }

        wireframe.enabled = !wireframe.enabled;
        playerCamera.clearFlags = wireframe.enabled?  CameraClearFlags.SolidColor: wireframe.orignalClearFlags;
    }

    private void ToggleNoClip()
    {
        var controller = GameObject.Find("FirstPersonController");
        var rigidBody = controller?.GetComponent<Rigidbody>();
        if (rigidBody != null)
            rigidBody.detectCollisions = !rigidBody.detectCollisions;
    }

    private List<ItemInfo> FindItemsByGroupId(string groupId)
    {
        var items = new List<ItemInfo>();
        foreach (var trackItemFlag in EditorUtils.FindFlagsByGroupId(groupId))
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
        Shared.Editor.RequestRefreshGui();
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

        var trackItemFlag = EditorUtils.FindFlagInParent(raycastHit.transform.gameObject);
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
            GridUtils.RoundVectorToStep(gizmo.transform.position, Shared.PlacementUtils.GridRound);
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

    private class WireframeCamera : MonoBehaviour
    {
        public CameraClearFlags orignalClearFlags;

        void OnPreRender()
        {
            GL.wireframe = true;
        }
        void OnPostRender()
        {
            GL.wireframe = false;
        }
    }

}