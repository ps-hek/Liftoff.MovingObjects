using System.Linq;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Liftoff.MovingObjects.Player;
using Liftoff.MovingObjects.Utils;
using UnityEngine.EventSystems;

namespace Liftoff.MovingObjects;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public sealed class Plugin : BaseUnityPlugin
{
    private static readonly ManualLogSource Log =
        BepInEx.Logging.Logger.CreateLogSource($"{PluginInfo.PLUGIN_NAME}.{nameof(Plugin)}");

    private static bool _animationResetInitialized;

    private Harmony _harmony;

    private void Awake()
    {
        Log.LogWarning($"Modification {PluginInfo.PLUGIN_NAME} {PluginInfo.PLUGIN_VERSION} loaded");
        _harmony = Harmony.CreateAndPatchAll(typeof(Plugin));
    }

    private void OnDestroy()
    {
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

        trackBuilderPanel.onItemSelected += animation.OnItemSelected;
        trackBuilderPanel.onItemSelectionCleared += animation.OnItemCleared;
    }


    [HarmonyPostfix]
    [HarmonyPatch(typeof(TrackEditorEditWindow), "AtLeastOneItemAvailable", typeof(TrackItemCategory))]
    private static void AtLeastOneItemAvailable(ref bool __result)
    {
        if (!__result)
            __result = true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(EventSystem), "IsPointerOverGameObject", typeof(int))]
    private static void OnIsPointerOverGameObject(ref bool __result)
    {
        if (!__result && GuiUtils.IsGuiLocked())
            __result = true;
    }

    private static void AddOnDroneResetHandler()
    {
        if (_animationResetInitialized)
            return;
        _animationResetInitialized = true;

        var flightManger = FindObjectsOfType<FlightManager>().Single();
        flightManger.onDroneResetDone += () =>
        {
            foreach (var player in FindObjectsOfType<AnimationPlayer>())
                player.Restart();
            foreach (var player in FindObjectsOfType<PhysicsPlayer>())
                player.Restart();
        };
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(LevelInitSequence), "InitializeLevel", typeof(Level))]
    private static void OnInitializeLevel(LevelInitSequence __instance, Level __0)
    {
        AddOnDroneResetHandler();
        if (__0.LevelFlags == LevelFlags.TrackEdit)
            return;

        void Callback()
        {
            OnGameModeInitialized();
            __instance.onGameModeInitialized -= Callback;
        }
        __instance.onGameModeInitialized += Callback;
    }

    private static void AddPhysics(TrackBlueprint blueprint, TrackItemFlag flag)
    {
        Log.LogWarning($"Item with physics detected: {blueprint}, {flag}");

        var physicsPlayer = flag.gameObject.AddComponent<PhysicsPlayer>();
        physicsPlayer.options = blueprint.mo_animationOptions;
    }

    private static void AddAnimation(TrackBlueprint blueprint, TrackItemFlag flag)
    {
        Log.LogWarning($"Item with animation detected: {blueprint}, {flag}");

        var player = flag.gameObject.AddComponent<AnimationPlayer>();
        player.steps = blueprint.mo_animationSteps;
        player.options = blueprint.mo_animationOptions;
    }

    private static void OnGameModeInitialized()
    {
        var flags = FindObjectsOfType<TrackItemFlag>();
        foreach (var flag in flags)
        {
            var blueprint = ReflectionUtils.GetPrivateFieldValueByType<TrackBlueprint>(flag);
            if (blueprint?.mo_animationOptions?.simulatePhysics == true)
                AddPhysics(blueprint, flag);
            else if (blueprint?.mo_animationSteps?.Count > 0)
                AddAnimation(blueprint, flag);
        }
    }
}