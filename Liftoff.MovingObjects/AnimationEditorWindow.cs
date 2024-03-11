using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Liftoff.MovingObjects.Player;
using Liftoff.MovingObjects.Utils;
using UnityEngine;
using UnityEngine.UIElements;
using Logger = BepInEx.Logging.Logger;

namespace Liftoff.MovingObjects;

internal class AnimationEditorWindow : MonoBehaviour
{
    public enum Type
    {
        None,
        Animation,
        Physics
    }

    private const string PlayButtonText = "Play";
    private const string StopButtonText = "Stop";

    private static readonly ManualLogSource Log =
        Logger.CreateLogSource($"{PluginInfo.PLUGIN_NAME}.{nameof(AnimationEditorWindow)}");

    private TrackBlueprint _blueprint;
    private MonoBehaviour _item;
    private VisualElement _root;
    private GameObject _tempAnimationObject;

    private GameObject _tempPhysicsObject;

    private UIDocument _uiDocument;

    public Assets assets;


    private MO_AnimationOptions options => _blueprint.mo_animationOptions;
    private List<MO_Animation> steps => _blueprint.mo_animationSteps;

    private void Awake()
    {
        _uiDocument = gameObject.AddComponent<UIDocument>();
        _uiDocument.visualTreeAsset = assets.VisualTreeAsset;
        _uiDocument.panelSettings = assets.PanelSettings;
        _uiDocument.rootVisualElement.StretchToParentSize();
    }

    private void OnEnable()
    {
        Log.LogInfo("OnEnable");

        _root = _uiDocument.rootVisualElement;

        _root.Q<DropdownField>("type")
            .RegisterValueChangedCallback(evt => OnSelectType(Enum.Parse<Type>(evt.newValue, true)));

        _root.Q<Toggle>("animation-teleport-to-start")
            .RegisterValueChangedCallback(evt => options.teleportToStart = evt.newValue);
        _root.Q<Button>("animation-add").clicked += () =>
        {
            steps.Add(new MO_Animation
            {
                delay = 0f,
                time = 1f,
                position = new SerializableVector3(_item.transform.position),
                rotation = new SerializableVector3(_item.transform.rotation.eulerAngles)
            });
            RefreshGui();
        };
        GuiUtils.ConvertToFloatField(_root.Q<TextField>("animation-warmup"),
            f => options.animationWarmupDelay = f);
        _root.Q<Button>("animation-play").clicked += OnPlayAnimationClicked;

        GuiUtils.ConvertToFloatField(_root.Q<TextField>("physics-time"),
            f => options.simulatePhysicsTime = f);
        GuiUtils.ConvertToFloatField(_root.Q<TextField>("physics-delay"),
            f => options.simulatePhysicsDelay = f);
        GuiUtils.ConvertToFloatField(_root.Q<TextField>("physics-warmup"),
            f => options.simulatePhysicsWarmupDelay = f);
        _root.Q<Button>("physics-play").clicked += OnPlayPhysicsClicked;

        RefreshGui();
    }

    private void OnPlayAnimationClicked()
    {
        if (_tempAnimationObject == null)
            StartAnimation();
        else
            StopAnimation();
        RefreshGui();
    }

    private void OnPlayPhysicsClicked()
    {
        if (_tempPhysicsObject == null)
            StartSimulation();
        else
            StopSimulation();
        RefreshGui();
    }

    private void RefreshGui()
    {
        var currentType = options == null ? Type.None : options.simulatePhysics ? Type.Physics : Type.Animation;
        _root.Q<DropdownField>("type").value = currentType.ToString();
        OnSelectType(currentType);

        switch (currentType)
        {
            case Type.Animation:
                _root.Q<Toggle>("animation-teleport-to-start").value = options.teleportToStart;
                _root.Q<TextField>("animation-warmup").value = GuiUtils.FloatToString(options.animationWarmupDelay);

                GuiUtils.SetVisible(_root.Q<Label>("animation-steps-empty"), steps.Count == 0);

                var animationPlay = _root.Q<Button>("animation-play");
                animationPlay.text = _tempAnimationObject == null ? PlayButtonText : StopButtonText;

                var stepsContainer = _root.Q<ScrollView>("animation-steps").contentContainer;
                stepsContainer.Clear();
                foreach (var step in steps)
                    AddStepElement(stepsContainer, step);
                break;
            case Type.Physics:
                _root.Q<TextField>("physics-time").value = GuiUtils.FloatToString(options.simulatePhysicsTime);
                _root.Q<TextField>("physics-delay").value = GuiUtils.FloatToString(options.simulatePhysicsDelay);
                _root.Q<TextField>("physics-warmup").value = GuiUtils.FloatToString(options.simulatePhysicsWarmupDelay);

                var physicsPlay = _root.Q<Button>("physics-play");
                physicsPlay.text = _tempPhysicsObject == null ? PlayButtonText : StopButtonText;
                break;
            case Type.None:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(currentType), currentType, null);
        }
    }

    private void AddStepElement(VisualElement stepsContainer, MO_Animation step)
    {
        var item = assets.AnimationTemplateAsset.Instantiate();

        item.Q<Label>("position").text = GuiUtils.VectorToString(step.position);
        item.Q<Label>("rotation").text = GuiUtils.VectorToString(step.rotation);
        item.Q<TextField>("time").value = GuiUtils.FloatToString(step.time);
        item.Q<TextField>("delay").value = GuiUtils.FloatToString(step.delay);

        GuiUtils.ConvertToFloatField(item.Q<TextField>("time"), f => step.time = f, step.time);
        GuiUtils.ConvertToFloatField(item.Q<TextField>("delay"), f => step.delay = f, step.delay);

        item.Q<Button>("delete").clicked += () =>
        {
            steps.Remove(step);
            RefreshGui();
        };
        stepsContainer.Add(item);
    }

    private void OnSelectType(Type type)
    {
        var animationBox = _root.Q<GroupBox>("animation-box");
        var physicsBox = _root.Q<GroupBox>("physics-box");

        switch (type)
        {
            case Type.None:
                _blueprint.mo_animationOptions = null;
                _blueprint.mo_animationSteps = null;
                GuiUtils.SetVisible(animationBox, false);
                GuiUtils.SetVisible(physicsBox, false);
                break;
            case Type.Animation:
                _blueprint.mo_animationOptions ??= new MO_AnimationOptions();
                _blueprint.mo_animationSteps ??= new List<MO_Animation>();

                GuiUtils.SetVisible(animationBox, true);
                GuiUtils.SetVisible(physicsBox, false);
                _blueprint.mo_animationOptions.simulatePhysics = false;
                StopSimulation();
                break;
            case Type.Physics:
                _blueprint.mo_animationOptions ??= new MO_AnimationOptions();
                _blueprint.mo_animationSteps ??= new List<MO_Animation>();

                GuiUtils.SetVisible(animationBox, false);
                GuiUtils.SetVisible(physicsBox, true);
                _blueprint.mo_animationOptions.simulatePhysics = true;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }

    public void OnItemSelected(MonoBehaviour item)
    {
        StopAnimation();
        StopSimulation();

        _item = item;
        _blueprint = ReflectionUtils.GetPrivateFieldValueByType<TrackBlueprint>(item);
        Invoke("RefreshGui", 0);

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

    private void OnDestroy()
    {
        StopAnimation();
        StopAnimation();
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

    public struct Assets
    {
        public VisualTreeAsset VisualTreeAsset { get; set; }
        public VisualTreeAsset AnimationTemplateAsset { get; set; }
        public PanelSettings PanelSettings { get; set; }
    }
}