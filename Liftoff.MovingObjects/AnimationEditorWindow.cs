using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Liftoff.MovingObjects.Player;
using Liftoff.MovingObjects.Utils;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using static Liftoff.FlightControllers.FlightMode;
using Button = UnityEngine.UIElements.Button;
using Logger = BepInEx.Logging.Logger;
using Toggle = UnityEngine.UIElements.Toggle;

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
    private const string NotSupportedButtonText = "CURRENTLY IN DEVELOPEMENT";

    private static readonly ManualLogSource Log =
        Logger.CreateLogSource($"{PluginInfo.PLUGIN_NAME}.{nameof(AnimationEditorWindow)}");

    private TrackBlueprint _blueprint;
    private MonoBehaviour _item;
    private VisualElement _root;
    private GameObject _tempAnimationObject;

    private GameObject _tempPhysicsObject;

    private UIDocument _uiDocument;

    public Assets assets;

    private MO_TriggerOptions trigger
    {
        get => _blueprint.mo_triggerOptions;
        set => _blueprint.mo_triggerOptions = value;
    }

    private MO_AnimationOptions options => _blueprint.mo_animationOptions;
    private List<MO_Animation> steps => _blueprint.mo_animationSteps;

    private void Awake()
    {
        _uiDocument = gameObject.AddComponent<UIDocument>();
        _uiDocument.visualTreeAsset = assets.VisualTreeAsset;
        _uiDocument.panelSettings = assets.PanelSettings;
        _uiDocument.rootVisualElement.StretchToParentSize();

        Shared.Editor.OnRefreshGuiRequest += RefreshGui;
    }

    private void OnEnable()
    {
        // Dirty hack for add focus support
        GameObject.Find("AnimationEditorWindowPanelSettings").AddComponent<InputField>().interactable = false;

        _root = _uiDocument.rootVisualElement;

        _root.Q<Toggle>("trigger-enabled")
            .RegisterValueChangedCallback(evt =>
            {
                if (!evt.newValue)
                    trigger = null;
                else if (trigger == null)
                    trigger = new MO_TriggerOptions();
                RefreshGui();
            });

        _root.Q<TextField>("trigger-name").RegisterValueChangedCallback(evt => trigger.triggerName = evt.newValue);
        _root.Q<TextField>("trigger-target").RegisterValueChangedCallback(evt => trigger.triggerTarget = evt.newValue);
        GuiUtils.ConvertToFloatField(_root.Q<TextField>("trigger-target-speed-min"),
            f => trigger.triggerMinSpeed = f);
        GuiUtils.ConvertToFloatField(_root.Q<TextField>("trigger-target-speed-max"),
            f => trigger.triggerMaxSpeed = f);

        _root.Q<DropdownField>("type")
            .RegisterValueChangedCallback(evt =>
            {
                SetType(Enum.Parse<Type>(evt.newValue, true));
                RefreshGui();
            });
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
        GuiUtils.ConvertToIntField(_root.Q<TextField>("animation-repeats"),
            i => options.animationRepeats = i);
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
        if (!string.IsNullOrEmpty(_blueprint.mo_groupId))
            return; // TODO: Fix group physics

        if (_tempPhysicsObject == null)
            StartSimulation();
        else
            StopSimulation();
        RefreshGui();
    }

    private IEnumerator UpdateMenuPos()
    {
        yield return new WaitForFixedUpdate();
        var rect = GameObject.Find("DetailWindows")?.GetComponent<RectTransform>();
        if (rect != null)
            _root.Q<VisualElement>("root").style.top =
                new StyleLength(new Length(rect.sizeDelta.y + 10, LengthUnit.Pixel));
    }


    private void RefreshGui()
    {
        if (_blueprint == null)
            return;

        StartCoroutine(UpdateMenuPos());

        var currentType = options == null ? Type.None : options.simulatePhysics ? Type.Physics : Type.Animation;
        _root.Q<DropdownField>("type").value = currentType.ToString();

        var hasTrigger = trigger != null;

        _root.Q<Toggle>("trigger-enabled").value = hasTrigger;
        GuiUtils.SetVisible(_root.Q<GroupBox>("trigger-box"), hasTrigger);
        if (hasTrigger)
        {
            var triggerName = _root.Q<TextField>("trigger-name");
            triggerName.value = trigger.triggerName;

            var triggerTarget = _root.Q<TextField>("trigger-target");
            triggerTarget.value = trigger.triggerTarget;

            var triggerTargetGroup = _root.Q<VisualElement>("trigger-target-section");

            GuiUtils.SetVisible(triggerName, true);
            GuiUtils.SetVisible(triggerTargetGroup, _blueprint.itemID.StartsWith("Checkpoint"));

            _root.Q<TextField>("trigger-target-speed-min").value = GuiUtils.FloatToString(trigger.triggerMinSpeed);
            _root.Q<TextField>("trigger-target-speed-max").value = GuiUtils.FloatToString(trigger.triggerMaxSpeed);
        }

        var animationBox = _root.Q<GroupBox>("animation-box");
        var physicsBox = _root.Q<GroupBox>("physics-box");

        switch (currentType)
        {
            case Type.Animation:
                GuiUtils.SetVisible(animationBox, true);
                GuiUtils.SetVisible(physicsBox, false);

                _root.Q<Toggle>("animation-teleport-to-start").value = options.teleportToStart;
                _root.Q<TextField>("animation-warmup").value = GuiUtils.FloatToString(options.animationWarmupDelay);
                _root.Q<TextField>("animation-repeats").value = options.animationRepeats.ToString();

                GuiUtils.SetVisible(_root.Q<Label>("animation-steps-empty"), steps.Count == 0);

                var animationPlay = _root.Q<Button>("animation-play");
                animationPlay.text = _tempAnimationObject == null ? PlayButtonText : StopButtonText;

                var stepsContainer = _root.Q<ScrollView>("animation-steps");
                stepsContainer.Clear();
                for (var i = 0; i < steps.Count; i++)
                    AddStepElement(stepsContainer, steps[i], i);
                break;
            case Type.Physics:
                GuiUtils.SetVisible(animationBox, false);
                GuiUtils.SetVisible(physicsBox, true);

                _root.Q<TextField>("physics-time").value = GuiUtils.FloatToString(options.simulatePhysicsTime);
                _root.Q<TextField>("physics-delay").value = GuiUtils.FloatToString(options.simulatePhysicsDelay);
                _root.Q<TextField>("physics-warmup").value = GuiUtils.FloatToString(options.simulatePhysicsWarmupDelay);

                var physicsPlay = _root.Q<Button>("physics-play");

                if (!string.IsNullOrEmpty(_blueprint.mo_groupId)) // TODO: Fix group physics
                    physicsPlay.text = NotSupportedButtonText;
                else
                    physicsPlay.text = _tempPhysicsObject == null ? PlayButtonText : StopButtonText;
                break;
            case Type.None:
                GuiUtils.SetVisible(animationBox, false);
                GuiUtils.SetVisible(physicsBox, false);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(currentType), currentType, null);
        }
    }

    private void AddStepElement(VisualElement stepsContainer, MO_Animation step, int i)
    {
        var item = assets.AnimationTemplateAsset.Instantiate();

        item.Q<Label>("id").text = i.ToString();
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

    private void SetType(Type type)
    {
        switch (type)
        {
            case Type.None:
                _blueprint.mo_animationOptions = null;
                _blueprint.mo_animationSteps = null;
                break;
            case Type.Animation:
                _blueprint.mo_animationOptions ??= new MO_AnimationOptions();
                _blueprint.mo_animationSteps ??= new List<MO_Animation>();
                _blueprint.mo_animationOptions.simulatePhysics = false;
                StopSimulation();
                break;
            case Type.Physics:
                _blueprint.mo_animationOptions ??= new MO_AnimationOptions();
                _blueprint.mo_animationSteps ??= new List<MO_Animation>();
                _blueprint.mo_animationSteps.Clear();
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
        Shared.Editor.ItemSelected(new Shared.Editor.ItemInfo
        {
            gameObject = item.gameObject,
            blueprint = _blueprint
        });

        Log.LogInfo(
            $"Item selected: {_blueprint.itemID}/{_blueprint.instanceID}, {_item.gameObject.transform.position}");
    }

    public void OnItemCleared()
    {
        StopAnimation();
        StopSimulation();
        _blueprint = null;
        Shared.Editor.ItemCleared();
        Log.LogInfo("Item unselected");
    }

    private void OnDestroy()
    {
        StopAnimation();
        StopSimulation();
    }

    private GameObject CreateTempObj()
    {
        if (string.IsNullOrEmpty(_blueprint.mo_groupId))
        {
            var obj = Instantiate(_item.gameObject);
            obj.transform.SetPositionAndRotation(_item.transform.position, _item.transform.rotation);
            
            obj.transform.localScale = _item.gameObject.transform.localScale;
            return obj;
        }

        var childs = new List<GameObject>();
        GameObject rootObj = null;
        var flags = EditorUtils.FindFlagsByGroupId(_blueprint.mo_groupId);
        foreach (var flag in flags)
        {
            var clone = Instantiate(flag.gameObject);
            clone.transform.SetPositionAndRotation(flag.gameObject.transform.position, flag.gameObject.transform.rotation);
            clone.transform.localScale = flag.gameObject.transform.localScale;
            if (flag.gameObject == _item.gameObject)
                rootObj = clone;
            else
                childs.Add(clone);
        }

        FakeGroup.GroupObjects(rootObj, childs, true);
        return rootObj;
    }

    private void StartAnimation()
    {
        Log.LogWarning($"Animation start: {_item.gameObject} at {_item.transform.position}");

        _tempAnimationObject = CreateTempObj();
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
        List<GameObject> groupObjects = null;
        if (!string.IsNullOrEmpty(_blueprint.mo_groupId))
            groupObjects = EditorUtils.FindFlagsByGroupId(_blueprint.mo_groupId).Select(c => c.gameObject).ToList();

        _tempPhysicsObject = CreateTempObj();

        var tempColliders = _tempPhysicsObject.GetComponentsInChildren<Collider>().ToList();

        var tempGroupObjects = FakeGroup.GetChilds(_tempPhysicsObject);
        if (tempGroupObjects != null)
            tempColliders.AddRange(tempGroupObjects.SelectMany(o => o.GetComponentsInChildren<Collider>()));
        
        var targetColliders = new List<Collider>();
        targetColliders.AddRange(_item.gameObject.GetComponentsInChildren<Collider>());
        targetColliders.AddRange(GameObject.Find("TrackEditorGizmo").GetComponentsInChildren<Collider>());
        if (groupObjects != null)
            targetColliders.AddRange(groupObjects.SelectMany(c => c.GetComponentsInChildren<Collider>()));

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