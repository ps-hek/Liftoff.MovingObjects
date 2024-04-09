using System.Linq;
using BepInEx.Logging;
using Liftoff.MovingObjects.Player;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace Liftoff.MovingObjects;

internal class TriggerName : MonoBehaviour
{
    public string triggerName;
}

internal class TriggerBehavior : MonoBehaviour
{
    private static readonly ManualLogSource Log =
        Logger.CreateLogSource($"{PluginInfo.PLUGIN_NAME}.{nameof(TriggerBehavior)}");

    private AnimationPlayer[] _animationPlayers;
    private PhysicsPlayer[] _physicsPlayers;
    private Vector3[] _teleportPositions;

    private Vector3 _teleportPos;
    private Rigidbody _teleportDrone;

    private bool _triggered;

    public float? triggerMaxSpeed;
    public float? triggerMinSpeed;
    public string triggerTarget;
    public bool triggerTeleport;

    private void Start()
    {
        var targetTriggers = FindObjectsByType<TriggerName>(FindObjectsSortMode.None)
            .Where(t => string.Equals(t.triggerName, triggerTarget)).ToArray();
        if (triggerTeleport)
            _teleportPositions = targetTriggers.Select(t => t.gameObject.transform.position).ToArray();
        _animationPlayers = targetTriggers.Select(t => t.GetComponent<AnimationPlayer>()).Where(p => p != null)
            .ToArray();
        _physicsPlayers = targetTriggers.Select(t => t.GetComponent<PhysicsPlayer>()).Where(p => p != null).ToArray();

        Log.LogInfo(
            $"Detected {_animationPlayers.Length} animations and {_physicsPlayers.Length} physics for '{triggerTarget}' trigger");
    }

    private static float MpsToKph(float kps)
    {
        return kps * 3.6f;
    }

    public void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer != LayerMask.NameToLayer("Drone") || _triggered)
            return;

        var speed = -1f;
        if (other.attachedRigidbody != null)
        {
            speed = MpsToKph(other.attachedRigidbody.velocity.magnitude);
            if (speed < triggerMinSpeed)
            {
                Log.LogInfo($"Trigger ignored {other}, speed {speed} < {triggerMinSpeed}");
                return;
            }

            if (speed > triggerMaxSpeed)
            {
                Log.LogInfo($"Trigger ignored {other}, speed {speed} > {triggerMaxSpeed}");
                return;
            }
        }

        _triggered = true;


        Log.LogInfo($"Triggered by {other}, speed {speed}");
        foreach (var player in _animationPlayers)
        {
            Log.LogInfo($"Triggered animation: {player} from '{triggerTarget}'");
            player.Trigger();
        }

        foreach (var player in _physicsPlayers)
        {
            Log.LogInfo($"Triggered physics: {player} from '{triggerTarget}'");
            player.Trigger();
        }

        if (_teleportPositions != null && other.attachedRigidbody != null && _teleportDrone == null)
        {
           _teleportDrone = other.attachedRigidbody;
           _teleportPos = _teleportPositions[Random.Range(0, _teleportPositions.Length)];
        }
    }

    public void OnTriggerExit(Collider other)
    {
        if (_triggered && other.gameObject.layer == LayerMask.NameToLayer("Drone"))
            _triggered = false;
    }

    private void LateUpdate()
    {
        if (_teleportDrone == null)
            return;


        _teleportDrone.position = _teleportPos;
        _teleportDrone = null;
    }
}