using System.Collections;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Liftoff.MovingObjects.Player;
using Liftoff.MovingObjects.Utils;
using UnityEngine;
using UnityEngine.UIElements;

namespace Liftoff.MovingObjects;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public sealed class Plugin : BaseUnityPlugin
{
    private static readonly ManualLogSource Log =
        BepInEx.Logging.Logger.CreateLogSource($"{PluginInfo.PLUGIN_NAME}.{nameof(Plugin)}");

    private static AssetBundle _assetBundle;
    private static AnimationEditorWindow.Assets _editorAssets;
    private static PlacementUtilsWindow.Assets _placementAssets;

    private Harmony _harmony;

    private void Awake()
    {
        Log.LogWarning($"Modification {PluginInfo.PLUGIN_NAME} {PluginInfo.PLUGIN_VERSION} loaded");
        _assetBundle = AssetBundle.LoadFromMemory(UI.LiftoffUI);
        _editorAssets = new AnimationEditorWindow.Assets
        {
            VisualTreeAsset =
                _assetBundle.LoadAsset<VisualTreeAsset>("Assets/Liftoff.MovingObject/AnimationEditorWindow.uxml"),
            AnimationTemplateAsset =
                _assetBundle.LoadAsset<VisualTreeAsset>("Assets/Liftoff.MovingObject/AnimationStepTemplate.uxml"),
            PanelSettings =
                _assetBundle.LoadAsset<PanelSettings>(
                    "Assets/Liftoff.MovingObject/AnimationEditorWindowPanelSettings.asset")
        };

        _placementAssets = new PlacementUtilsWindow.Assets
        {
            VisualTreeAsset =
                _assetBundle.LoadAsset<VisualTreeAsset>("Assets/Liftoff.MovingObject/UtilsWindow.uxml"),
            PanelSettings =
                _assetBundle.LoadAsset<PanelSettings>(
                    "Assets/Liftoff.MovingObject/UtilsWindowPanelSettings.asset")
        };
        _harmony = Harmony.CreateAndPatchAll(typeof(Plugin));
    }

    private void OnDestroy()
    {
        _assetBundle.Unload(true);
        _harmony?.UnpatchSelf();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TrackEditorGUI), "Start")]
    private static void OnTrackEditorGuiStart(TrackEditorGUI __instance)
    {
        var trackMenu = ReflectionUtils.GetPrivateFieldValue<TrackEditorMenuManager>(__instance, "trackMenu");
        var trackBuilderPanel =
            ReflectionUtils.GetPrivateFieldValue<TrackEditorEditWindow>(trackMenu, "trackBuilderPanel");

        var animation = trackBuilderPanel.detailPane.gameObject.AddComponent<AnimationEditorWindow>();
        animation.assets = _editorAssets;

        trackBuilderPanel.onItemSelected += animation.OnItemSelected;
        trackBuilderPanel.onItemSelectionCleared += animation.OnItemCleared;

        var placementUtilsObj = new GameObject("MO_PlacementUtils");
        placementUtilsObj.transform.SetParent(trackBuilderPanel.gameObject.transform);


        var placementUtilsWindow = placementUtilsObj.AddComponent<PlacementUtilsWindow>();
        placementUtilsWindow.assets = _placementAssets;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TrackEditorEditWindow), "AtLeastOneItemAvailable", typeof(TrackItemCategory))]
    private static void AtLeastOneItemAvailable(ref bool __result)
    {
        if (!__result)
            __result = true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TrackDragCenterOnCamera), "OnDragHold")]
    [HarmonyPatch(typeof(TrackDragBehaviorSnap), "OnDragHold")]
    [HarmonyPatch(typeof(TrackDragBehaviorRibbon), "OnDragHold")]
    private static void OnDragHold(TrackDragCenterOnCamera __instance)
    {
        if (Shared.PlacementUtils.DragGridRound <= 0)
            return;
        var parent = __instance.gameObject.transform.parent;
        if (parent == null)
            return;
        parent.position = GirdUtils.RoundVectorToStep(parent.position, Shared.PlacementUtils.DragGridRound);
    }


    [HarmonyPostfix]
    [HarmonyPatch(typeof(FlightManager), "ResetDroneRoutine")]
    private static IEnumerator ResetDroneRoutine(IEnumerator __result)
    {
        Log.LogInfo("Drone reset start");

        var animations = FindObjectsOfType<AnimationPlayer>();
        var physics = FindObjectsOfType<PhysicsPlayer>();

        foreach (var player in animations)
        {
            player.enabled = false;
            player.Restart();
        }

        foreach (var player in physics)
        {
            player.enabled = false;
            player.Restart();
        }

        while (__result.MoveNext())
            yield return __result.Current;

        Log.LogInfo("Drone reset done");
        foreach (var player in animations)
            player.enabled = true;
        foreach (var player in physics)
            player.enabled = true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(LevelInitSequence), "InitializeLevel", typeof(Level))]
    private static void OnInitializeLevel(LevelInitSequence __instance, Level __0)
    {
        if (__0.LevelFlags == LevelFlags.TrackEdit)
            return;

        void Callback()
        {
            OnGameModeInitialized();
            __instance.onGameModeInitialized -= Callback;
        }

        __instance.onGameModeInitialized += Callback;
    }

    private static void AddPhysics(TrackBlueprint blueprint, Component flag, bool waitForTrigger)
    {
        Log.LogWarning($"Item with physics detected: {blueprint}, {flag}");

        var player = flag.gameObject.AddComponent<PhysicsPlayer>();
        player.options = blueprint.mo_animationOptions;
        player.waitForTrigger = waitForTrigger;

        var collider = flag.GetComponentInChildren<Collider>();
        if (collider?.enabled == false)
        {
            collider.enabled = true;
            collider.gameObject.layer = LayerMask.NameToLayer("Ghost");
        }
    }

    private static void AddAnimation(TrackBlueprint blueprint, Component flag, bool waitForTrigger)
    {
        Log.LogWarning($"Item with animation detected: {blueprint}, {flag}");

        var player = flag.gameObject.AddComponent<AnimationPlayer>();
        player.steps = blueprint.mo_animationSteps;
        player.options = blueprint.mo_animationOptions;
        player.waitForTrigger = waitForTrigger;
    }

    private static bool AddTrigger(TrackBlueprint blueprint, Component flag)
    {
        var options = blueprint.mo_triggerOptions;
        Log.LogWarning($"Item with trigger detected: {options.triggerTarget}/{options.triggerName}, {flag}");

        var waitForTrigger = false;
        if (!string.IsNullOrEmpty(options.triggerName))
        {
            flag.gameObject.AddComponent<TriggerName>().triggerName = options.triggerName;
            waitForTrigger = true;
        }

        if (!string.IsNullOrEmpty(options.triggerTarget))
        {
            var checkpointTrigger = flag.gameObject.transform.Find("CheckpointTrigger");
            if (checkpointTrigger != null)
            {
                var trigger = checkpointTrigger.gameObject.AddComponent<TriggerBehavior>();
                trigger.triggerTarget = options.triggerTarget;
                if (options.triggerMinSpeed > 0)
                    trigger.triggerMinSpeed = options.triggerMinSpeed;
                if (options.triggerMaxSpeed > 0)
                    trigger.triggerMaxSpeed = options.triggerMaxSpeed;
            }
        }

        return waitForTrigger;
    }

    private static void InjectPlayers(IEnumerable<Component> flags)
    {
        foreach (var flag in flags)
        {
            var blueprint = ReflectionUtils.GetPrivateFieldValueByType<TrackBlueprint>(flag);

            var waitForTrigger = false;
            if (blueprint?.mo_triggerOptions != null)
                waitForTrigger = AddTrigger(blueprint, flag);

            if (blueprint?.mo_animationOptions?.simulatePhysics == true)
                AddPhysics(blueprint, flag, waitForTrigger);
            else if (blueprint?.mo_animationSteps?.Count > 0)
                AddAnimation(blueprint, flag, waitForTrigger);
        }
    }

    private static void OnGameModeInitialized()
    {
        InjectPlayers(FindObjectsOfType<TrackItemFlag>());
        InjectPlayers(FindObjectsOfType<TrackItemKillDroneTrigger>());
        InjectPlayers(FindObjectsOfType<TrackItemShowTextTrigger>());
        InjectPlayers(FindObjectsOfType<TrackItemPlaySoundTrigger>());
        InjectPlayers(FindObjectsOfType<TrackItemRepairPropellersTrigger>());
        InjectPlayers(FindObjectsOfType<TrackItemChargeBatteryTrigger>());
        InjectPlayers(FindObjectsOfType<TrackItemFlexibleCheckpointTrigger>());
    }
}