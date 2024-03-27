using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Liftoff.MovingObjects.Utils;

internal class FakeGroup
{
    public static IDisposable GroupObjects(GameObject rootGameObject, List<GameObject> childs, bool destroyChilds)
    {
        var ctx = new FakeContext();
        foreach (var gameObject in childs)
        {
            Cleanup(rootGameObject);
            var fakeChild = gameObject.AddComponent<FakeChild>();
            fakeChild.fakeParent = rootGameObject.transform;
            ctx.childs.Add(fakeChild);
        }

        if (destroyChilds)
        {
            Cleanup(rootGameObject);
            var fakeParent = rootGameObject.AddComponent<FakeParent>();
            fakeParent.childs = childs;
            ctx.parent = fakeParent;
        }

        return ctx;
    }

    public static IReadOnlyList<GameObject> GetChilds(GameObject gameObject)
    {
        var parent = gameObject.GetComponent<FakeParent>();
        return parent != null ? parent.childs : null;
    }

    private static void Cleanup(GameObject gameObject)
    {
        var child = gameObject.GetComponent<FakeChild>();
        if (child != null)
            Object.Destroy(child);
        var parent = gameObject.GetComponent<FakeParent>();
        if (parent != null)
            Object.Destroy(parent);
    }

    private class FakeContext : IDisposable
    {
        public readonly List<FakeChild> childs = new();
        public FakeParent parent;

        public void Dispose()
        {
            if (parent != null)
                Object.Destroy(parent);
            foreach (var child in childs)
                Object.Destroy(child);
        }
    }

    private class FakeParent : MonoBehaviour
    {
        public IReadOnlyList<GameObject> childs;

        private void OnDestroy()
        {
            foreach (var children in childs)
                Destroy(children);
        }
    }

    private class FakeChild : MonoBehaviour
    {
        public Transform fakeParent;

        private Matrix4x4 parentMatrix;

        private Vector3 startChildPosition;
        private Quaternion startChildRotationQ;


        private Vector3 startParentPosition;
        private Quaternion startParentRotationQ;
        private Vector3 startParentScale;

        private void Start()
        {
            startParentPosition = fakeParent.position;
            startParentRotationQ = fakeParent.rotation;
            startParentScale = fakeParent.lossyScale;

            startChildPosition = transform.position;
            startChildRotationQ = transform.rotation;

            startChildPosition =
                DivideVectors(Quaternion.Inverse(fakeParent.rotation) * (startChildPosition - startParentPosition),
                    startParentScale);
        }

        private static Vector3 DivideVectors(Vector3 num, Vector3 den)
        {
            return new Vector3(num.x / den.x, num.y / den.y, num.z / den.z);
        }

        private void Update()
        {
            parentMatrix = Matrix4x4.TRS(fakeParent.position, fakeParent.rotation, fakeParent.lossyScale);
            transform.position = parentMatrix.MultiplyPoint3x4(startChildPosition);
            transform.rotation = fakeParent.rotation * Quaternion.Inverse(startParentRotationQ) * startChildRotationQ;
        }
    }
}