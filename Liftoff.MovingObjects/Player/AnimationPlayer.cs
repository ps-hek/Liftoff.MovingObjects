using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Liftoff.MovingObjects.Player;

internal  sealed class AnimationPlayer : MonoBehaviour
{
    private const float MinDistance = 0.001f;
    private int _currentStep;
    private float _currentStepDistance;
    private Quaternion _currentStepRotation;
    private Vector3 _initPosition;

    private bool _paused;
    private Quaternion _initRotation;

    public MO_AnimationOptions options;
    public List<MO_Animation> steps;
    private Step[] _stepsCached;
    private Rigidbody _rigidBody;

    private void Start()
    {
        _stepsCached = steps.Select(animation => new Step(animation)).ToArray();
        _rigidBody = gameObject.AddComponent<Rigidbody>();
        _rigidBody.isKinematic = true;

        _initPosition = transform.position;
        _initRotation = transform.rotation;

        _currentStepRotation = transform.rotation;
        _currentStepDistance = CalculateSafeDistance(_stepsCached[0].Position);
    }

    private float CalculateSafeDistance(Vector3 pos2)
    {
        var dist = Vector3.Distance(transform.position, pos2);
        if (dist == 0f)
            dist = Mathf.Epsilon;
        return dist;
    }

    private IEnumerator WaitAndResume(float delay)
    {
        _paused = true;
        yield return new WaitForSeconds(delay);
        _paused = false;
    }

    private void Update()
    {
        if (_paused)
            return;

        var step = _stepsCached[_currentStep];
        var distance = Vector3.Distance(transform.position, step.Position);
        if (Mathf.Abs(distance) > MinDistance)
        {
            var pos = Vector3.MoveTowards(transform.position, step.Position, step.Speed * Time.deltaTime);
            var rot = Quaternion.Lerp(_currentStepRotation, step.Rotation, 1 - distance / _currentStepDistance);
            _rigidBody.MovePosition(pos);
            _rigidBody.MoveRotation(rot);
            return;
        }

        _currentStep++;
        if (_currentStep >= steps.Count)
        {
            _currentStep = 0;
            if (steps.Count > 0 && options?.teleportToStart == true)
            {
                var firstStep = new Step(steps[0]);
                _rigidBody.MovePosition(firstStep.Position);
                _rigidBody.MoveRotation(firstStep.Rotation);
            }
        }

        _currentStepRotation = transform.rotation;
        _currentStepDistance = CalculateSafeDistance(_stepsCached[_currentStep].Position);
        
        var delay = _stepsCached[_currentStep].Delay;
        if (delay > 0)
            StartCoroutine(WaitAndResume(delay));
    }

    public void Restart()
    {
        _currentStep = 0;
        _paused = false;

        transform.position = _initPosition;
        transform.rotation = _initRotation;
    }

    private struct Step
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly float Speed;
        public readonly float Delay;

        private static Vector3 ToVector3(SerializableVector3 serializableVector3)
        {
            return new Vector3(serializableVector3.x, serializableVector3.y, serializableVector3.z);
        }

        public Step(MO_Animation animation)
        {
            Position = ToVector3(animation.position);
            Rotation = Quaternion.Euler(ToVector3(animation.rotation));
            Speed = animation.speed;
            Delay = animation.delay;
        }
    }
}