using System.Collections;
using System.Collections.Generic;
using BepInEx.Logging;
using Liftoff.MovingObjects.Player;
using Liftoff.MovingObjects.Utils;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace Liftoff.MovingObjects;

public class AnimationEditorWindow : MonoBehaviour
{
    private const int BasePadding = 25;

    private static readonly ManualLogSource Log =
        Logger.CreateLogSource($"{PluginInfo.PLUGIN_NAME}.{nameof(AnimationEditorWindow)}");

    private static readonly Vector2 BaseResolution = new(3840, 2160);
    private static readonly Vector2 BaseSize = new(600, 940);


    private TrackBlueprint _blueprint;

    private Matrix4x4 _cachedMatrix;
    private MonoBehaviour _item;
    private Vector2Int _lastScreenSize;

    private Vector2 _scrollPosition;

    private GameObject _tempAnimationObject;
    private GameObject _tempPhysicsObject;

    private Rect _windowRect;

    private void Awake()
    {
        _windowRect = new Rect(BaseResolution.x - BaseSize.x - BasePadding, BaseResolution.y / 2f - BaseSize.y / 2,
            BaseSize.x, BaseSize.y);
        CalculateScaling();
    }

    private void CalculateScaling()
    {
        if (_lastScreenSize.x == Screen.width && _lastScreenSize.y == Screen.height)
            return;
        var xRatio = Screen.width / BaseResolution.x;
        var yRatio = Screen.height / BaseResolution.y;

        _cachedMatrix = Matrix4x4.TRS(new Vector3(0, 0, 0), Quaternion.identity, new Vector3(xRatio, yRatio, 1));
        _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
    }

    private void OnGUI()
    {
        if (_blueprint == null)
            return;
        CalculateScaling();

        var originalMatrix = GUI.matrix;
        GUI.matrix = _cachedMatrix;

        GUI.backgroundColor = Color.black;
        GUI.Window(0, _windowRect, DoAnimationWindow, "Animation");

        GUI.matrix = originalMatrix;
    }

    private void StartSimulation()
    {
        _tempPhysicsObject = Instantiate(_item.gameObject);
        _tempPhysicsObject.gameObject.transform.SetPositionAndRotation(_item.transform.position,
            _item.transform.rotation);

        var tempColliders = _tempPhysicsObject.GetComponentsInChildren<Collider>();
        var targetColliders = new List<Collider>();
        targetColliders.AddRange(_item.gameObject.GetComponentsInChildren<Collider>());
        targetColliders.AddRange(GameObject.Find("TrackEditorGizmo").GetComponentsInChildren<Collider>());

        foreach (var tempCollider in tempColliders)
        foreach (var targetCollider in targetColliders)
            Physics.IgnoreCollision(tempCollider, targetCollider);

        var player = _tempPhysicsObject.AddComponent<PhysicsPlayer>();
        player.options = _blueprint.mo_animationOptions;
    }

    private void StopSimulation()
    {
        if (_tempPhysicsObject != null)
        {
            Destroy(_tempPhysicsObject);
            _tempPhysicsObject = null;
        }
    }

    private void StartAnimation()
    {
        Log.LogWarning($"Animation start: {_item.gameObject} at {_item.transform.position}");

        _tempAnimationObject = Instantiate(_item.gameObject);
        _tempAnimationObject.transform.SetPositionAndRotation(_item.transform.position, _item.transform.rotation);

        var player = _tempAnimationObject.AddComponent<AnimationPlayer>();
        player.steps = new List<MO_Animation>(_blueprint.mo_animationSteps);
        player.options = _blueprint.mo_animationOptions;
    }

    private void StopAnimation()
    {
        if (_tempAnimationObject != null)
        {
            Destroy(_tempAnimationObject);
            _tempAnimationObject = null;
        }
    }

    private void DoAnimationWindow(int _)
    {
        var hasAnimation = _blueprint.mo_animationSteps is { Count: > 0 };
        _blueprint.mo_animationOptions ??= new MO_AnimationOptions();

        GUILayout.BeginHorizontal();
        {
            GUI.enabled = !_blueprint.mo_animationOptions.simulatePhysics;
            GUILayout.BeginVertical();
            if (GUILayout.Button("Add step"))
            {
                _blueprint.mo_animationSteps ??= new List<MO_Animation>();

                _blueprint.mo_animationSteps.Add(new MO_Animation
                {
                    delay = 0f,
                    time = 1f,
                    position = new SerializableVector3(_item.transform.position),
                    rotation = new SerializableVector3(_item.transform.rotation.eulerAngles)
                });
            }

            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            GUI.enabled = hasAnimation && GUI.enabled;
            if (GUILayout.Button(_tempAnimationObject == null ? "Play" : "Stop"))
            {
                if (_tempAnimationObject == null)
                    StartAnimation();
                else
                    StopAnimation();
            }

            GUI.enabled = true;
            GUILayout.EndVertical();

            GUI.enabled = true;
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        {
            GUI.enabled = hasAnimation;
            _blueprint.mo_animationOptions.teleportToStart =
                GUILayout.Toggle(_blueprint.mo_animationOptions.teleportToStart, "Teleport to start");
            GUI.enabled = true;
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        {
            GUILayout.BeginVertical();
            _blueprint.mo_animationOptions.simulatePhysics =
                GUILayout.Toggle(_blueprint.mo_animationOptions.simulatePhysics, "Simulate physics");
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            GuiUtils.TextBoxFloat("Time:", ref _blueprint.mo_animationOptions.simulatePhysicsTime,
                _blueprint.mo_animationOptions.simulatePhysics);
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            GuiUtils.TextBoxFloat("Delay:", ref _blueprint.mo_animationOptions.simulatePhysicsDelay,
                _blueprint.mo_animationOptions.simulatePhysics);
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            GuiUtils.TextBoxFloat("Warmup:", ref _blueprint.mo_animationOptions.simulatePhysicsWarmupDelay,
                _blueprint.mo_animationOptions.simulatePhysics);
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            GUI.enabled = _blueprint.mo_animationOptions.simulatePhysics;
            if (GUILayout.Button(_tempPhysicsObject == null ? "Simulate physics" : "Stop simulation"))
            {
                if (_tempPhysicsObject == null)
                    StartSimulation();
                else
                    StopSimulation();
            }

            GUI.enabled = true;
            GUILayout.EndVertical();
        }
        GUILayout.EndHorizontal();


        if (!hasAnimation)
            return;

        GUILayout.BeginHorizontal();
        _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

        MO_Animation stepToDelete = null;
        for (var i = 0; i < _blueprint.mo_animationSteps.Count; i++)
        {
            var step = _blueprint.mo_animationSteps[i];
            GUILayout.BeginHorizontal();
            {
                GUILayout.Box($"Step {i}");


                GUILayout.BeginVertical();
                GUILayout.Label($"Position: {step.position.x:F3}, {step.position.y:F3}, {step.position.z:F3}");
                GUILayout.Label($"Rotation: {step.rotation.x:F3}, {step.rotation.y:F3}, {step.rotation.z:F3}");
                GUILayout.EndVertical();

                GUILayout.BeginVertical();

                GUILayout.BeginHorizontal();
                GuiUtils.TextBoxFloat("Delay:", ref step.delay);
                GUILayout.EndHorizontal();


                GUILayout.BeginHorizontal();
                GuiUtils.TextBoxFloat("Time:", ref step.time);
                GUILayout.EndHorizontal();

                GUILayout.EndVertical();


                GUILayout.BeginVertical();
                if (GUILayout.Button("Delete"))
                    stepToDelete = step;
                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.EndScrollView();

        GUILayout.EndHorizontal();

        if (stepToDelete != null)
            _blueprint.mo_animationSteps.Remove(stepToDelete);
    }

    public void OnItemSelected(MonoBehaviour item)
    {
        _item = item;
        _blueprint = ReflectionUtils.GetPrivateFieldValueByType<TrackBlueprint>(item);
        Log.LogInfo(
            $"Item selected: {_blueprint.itemID}/{_blueprint.instanceID}, {_item.gameObject.transform.position}");
    }

    public void OnItemCleared()
    {
        StopAnimation();
        StopSimulation();
        _blueprint = null;
        Log.LogInfo("Item unselected");
    }

    private void OnDisable()
    {
        GuiUtils.Unlock();
        StopAnimation();
        StopSimulation();
    }

    private void OnEnable()
    {
        var leftTop = _cachedMatrix.MultiplyPoint3x4(new Vector3(_windowRect.x, _windowRect.y, 1));
        var rightBottom =
            _cachedMatrix.MultiplyPoint3x4(new Vector3(_windowRect.x + _windowRect.width,
                _windowRect.y + _windowRect.height,
                1));

        var rect = new Rect(leftTop.x, leftTop.y, rightBottom.x - leftTop.x, rightBottom.y - leftTop.y);
        Log.LogInfo($"Gui locked: {rect}");
        GuiUtils.Lock(rect);
    }

    private void OnDestroy()
    {
        StopAnimation();
        StopAnimation();
        GuiUtils.Unlock();
    }
}