using System;
using BepInEx.Logging;
using Liftoff.MovingObjects.Utils;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;
using Logger = BepInEx.Logging.Logger;

namespace Liftoff.MovingObjects;

internal class PlacementUtilsWindow : MonoBehaviour
{
    private static readonly ManualLogSource Log =
        Logger.CreateLogSource($"{PluginInfo.PLUGIN_NAME}.{nameof(PlacementUtilsWindow)}");

    private VisualElement _root;
    private UIDocument _uiDocument;

    public Assets assets;

    private void Awake()
    {
        _uiDocument = gameObject.AddComponent<UIDocument>();
        _uiDocument.visualTreeAsset = assets.VisualTreeAsset;
        _uiDocument.panelSettings = assets.PanelSettings;
        _uiDocument.rootVisualElement.StretchToParentSize();
    }

    private void OnEnable()
    {
        // Dirty hack for add focus support
        GameObject.Find("UtilsWindowPanelSettings").AddComponent<InputField>().interactable = false;

        _root = _uiDocument.rootVisualElement;
        GuiUtils.SetVisible(_root, false);

        GuiUtils.ConvertToFloatField(_root.Q<TextField>("grid-align-value"),
            f => AnimationEditorWindow.SharedState.GridRound = f, AnimationEditorWindow.SharedState.GridRound);
        _root.Q<Button>("grid-align").clicked += RoundGizmoLocation;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
            RoundGizmoLocation();
        else if (_root != null && Input.GetKeyDown(KeyCode.F2))
            GuiUtils.ToggleVisible(_root);
    }

    private static float RoundToStep(float value, float step, int decimals = 3)
    {
        if (step == 0)
            return value;

        var d = MathF.Pow(10, decimals);
        var rawVal = MathF.Round(value / step, MidpointRounding.AwayFromZero) * step;

        return MathF.Floor(rawVal * d) / d;
    }

    private static Vector3 RoundVectorToStep(Vector3 value, float step)
    {
        return new Vector3(RoundToStep(value.x, step), RoundToStep(value.y, step), RoundToStep(value.z, step));
    }

    private static void RoundGizmoLocation()
    {
        var gizmo = GameObject.Find("TrackEditorGizmo");
        if (gizmo == null)
            return;

        gizmo.transform.position =
            RoundVectorToStep(gizmo.transform.position, AnimationEditorWindow.SharedState.GridRound);
    }

    public struct Assets
    {
        public VisualTreeAsset VisualTreeAsset { get; set; }
        public PanelSettings PanelSettings { get; set; }
    }
}