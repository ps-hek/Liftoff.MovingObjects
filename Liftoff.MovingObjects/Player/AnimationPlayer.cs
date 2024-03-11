using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Liftoff.MovingObjects.Player;

internal sealed class AnimationPlayer : MonoBehaviour
{
    private Coroutine _animationCoroutine;
    private Vector3 _initPosition;
    private Quaternion _initRotation;
    private Rigidbody _rigidBody;

    private Step[] _stepsCached;

    public MO_AnimationOptions options;
    public List<MO_Animation> steps;

    private void Start()
    {
        _stepsCached = steps.Select(animation => new Step(animation)).ToArray();
        _rigidBody = gameObject.AddComponent<Rigidbody>();
        _rigidBody.isKinematic = true;

        _initPosition = transform.position;
        _initRotation = transform.rotation;

        StartAnimationLoop();
    }

    private void StartAnimationLoop()
    {
        _animationCoroutine = StartCoroutine(AnimationLoop());
    }

    private IEnumerator AnimationLoop()
    {
        while (true)
            yield return PlayAnimation();
    }

    private IEnumerator PlayAnimation()
    {
        if (options.animationWarmupDelay > 0)
            yield return new WaitForSeconds(options.animationWarmupDelay);

        for (var i = 0; i < _stepsCached.Length; i++)
        {
            var step = _stepsCached[i];
            if (step.Time <= 0f || (i == 0 && options.teleportToStart))
            {
                MoveRigidBody(step.Position, step.Rotation);
                yield return null;
            }
            else
            {
                if (step.Delay > 0)
                    yield return new WaitForSeconds(step.Delay);
                yield return MoveObject(step.Position, step.Rotation, step.Time);
            }
        }
    }

    private IEnumerator MoveObject(Vector3 targetPosition, Quaternion targetRotation, float duration)
    {
        var elapsed = 0f;
        var startPosition = transform.position;
        var startRotation = transform.rotation;

        while (elapsed < duration)
        {
            var t = elapsed / duration;
            MoveRigidBody(Vector3.Lerp(startPosition, targetPosition, t),
                Quaternion.Lerp(startRotation, targetRotation, t));
            elapsed += Time.deltaTime;
            yield return null;
        }

        MoveRigidBody(targetPosition, targetRotation);
        yield return null;
    }

    private void MoveRigidBody(Vector3 targetPosition, Quaternion targetRotation)
    {
        _rigidBody.MovePosition(targetPosition);
        _rigidBody.MoveRotation(targetRotation);
    }

    public void Restart()
    {
        StopCoroutine(_animationCoroutine);

        _rigidBody.position = _initPosition;
        _rigidBody.rotation = _initRotation;

        StartAnimationLoop();
    }

    private struct Step
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly float Time;
        public readonly float Delay;

        private static Vector3 ToVector3(SerializableVector3 serializableVector3)
        {
            return new Vector3(serializableVector3.x, serializableVector3.y, serializableVector3.z);
        }

        public Step(MO_Animation animation)
        {
            Position = ToVector3(animation.position);
            Rotation = Quaternion.Euler(ToVector3(animation.rotation));
            Time = animation.time;
            Delay = animation.delay;
        }
    }
}