using System.Collections;
using UnityEngine;

namespace Liftoff.MovingObjects.Player;

internal sealed class PhysicsPlayer : MonoBehaviour
{
    private Vector3 _initPosition;
    private Quaternion _initRotation;
    private Coroutine _physicsCoroutine;
    private Rigidbody _rigidBody;

    public MO_AnimationOptions options;

    private void Start()
    {
        _initPosition = transform.position;
        _initRotation = transform.rotation;

        _rigidBody = gameObject.AddComponent<Rigidbody>();
        _rigidBody.mass = float.MaxValue;
        _rigidBody.isKinematic = true;

        Restart();
    }

    private IEnumerator StartPhysics()
    {
        if (options.simulatePhysicsWarmupDelay > 0)
            yield return new WaitForSeconds(options.simulatePhysicsWarmupDelay);

        while (true)
        {
            if (options.simulatePhysicsDelay > 0)
                yield return new WaitForSeconds(options.simulatePhysicsDelay);

            _rigidBody.isKinematic = false;

            if (options.simulatePhysicsTime == 0)
                yield break;

            yield return new WaitForSeconds(options.simulatePhysicsTime);

            ResetPosition();
        }
    }

    private void ResetPosition()
    {
        _rigidBody.isKinematic = true;
        _rigidBody.velocity = Vector3.zero;
        _rigidBody.angularVelocity = Vector3.zero;
        _rigidBody.position = _initPosition;
        _rigidBody.rotation = _initRotation;
    }

    public void Restart()
    {
        if (_physicsCoroutine != null)
            StopCoroutine(_physicsCoroutine);

        ResetPosition();
        _physicsCoroutine = StartCoroutine(StartPhysics());
    }
}