﻿using System.Collections;
using UnityEngine;

namespace Liftoff.MovingObjects.Player;

internal sealed class PhysicsPlayer : MonoBehaviour
{
    private Rigidbody _rigidBody;
    private Vector3 _initPosition;
    private Quaternion _initRotation;
    private Coroutine _physicsCoroutine;

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
        _rigidBody.MovePosition(_initPosition);
        _rigidBody.MoveRotation(_initRotation);
    }

    public void Restart()
    {
        if (_physicsCoroutine !=null)
            StopCoroutine(_physicsCoroutine);

        ResetPosition();
        _physicsCoroutine = StartCoroutine(StartPhysics());
    }
}