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


    private bool _triggered;

    public string triggerTarget;

    private void Start()
    {
        var targetTriggers = FindObjectsByType<TriggerName>(FindObjectsSortMode.None)
            .Where(t => string.Equals(t.triggerName, triggerTarget)).ToArray();
        _animationPlayers = targetTriggers.Select(t => t.GetComponent<AnimationPlayer>()).Where(p => p != null)
            .ToArray();
        _physicsPlayers = targetTriggers.Select(t => t.GetComponent<PhysicsPlayer>()).Where(p => p != null).ToArray();

        Log.LogInfo(
            $"Detected {_animationPlayers.Length} animations and {_physicsPlayers.Length} physics for '{triggerTarget}' trigger");
    }

    public void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer != LayerMask.NameToLayer("Drone") || _triggered)
            return;
        _triggered = true;
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
    }

    public void OnTriggerExit(Collider other)
    {
        if (_triggered && other.gameObject.layer == LayerMask.NameToLayer("Drone"))
            _triggered = false;
    }
}