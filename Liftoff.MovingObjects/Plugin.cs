﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Liftoff.MovingObjects.Player;
using Liftoff.MovingObjects.Utils;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Liftoff.MovingObjects;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public sealed class Plugin : BaseUnityPlugin
{
    private static readonly ManualLogSource Log =
        BepInEx.Logging.Logger.CreateLogSource($"{PluginInfo.PLUGIN_NAME}.{nameof(Plugin)}");

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

    [HarmonyPostfix]
    [HarmonyPatch(typeof(FlightManager), "ResetDroneRoutine")]
    static IEnumerator ResetDroneRoutine(IEnumerator __result)
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

    private static void AddPhysics(TrackBlueprint blueprint, Component flag)
    {
        Log.LogWarning($"Item with physics detected: {blueprint}, {flag}");

        var physicsPlayer = flag.gameObject.AddComponent<PhysicsPlayer>();
        physicsPlayer.options = blueprint.mo_animationOptions;

        var collider = flag.GetComponentInChildren<Collider>();
        if (collider?.enabled == false)
        {
            collider.enabled = true;
            collider.gameObject.layer = LayerMask.NameToLayer("Ghost");
        }
    }

    private static void AddAnimation(TrackBlueprint blueprint, Component flag)
    {
        Log.LogWarning($"Item with animation detected: {blueprint}, {flag}");

        var player = flag.gameObject.AddComponent<AnimationPlayer>();
        player.steps = blueprint.mo_animationSteps;
        player.options = blueprint.mo_animationOptions;
    }

    private static void InjectPlayers(IEnumerable<Component> flags)
    {
        foreach (var flag in flags)
        {
            var blueprint = ReflectionUtils.GetPrivateFieldValueByType<TrackBlueprint>(flag);
            if (blueprint?.mo_animationOptions?.simulatePhysics == true)
                AddPhysics(blueprint, flag);
            else if (blueprint?.mo_animationSteps?.Count > 0)
                AddAnimation(blueprint, flag);
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
    }
}