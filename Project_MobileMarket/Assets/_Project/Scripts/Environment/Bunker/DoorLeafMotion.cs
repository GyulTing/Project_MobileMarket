using System;
using System.Collections;
using UnityEngine;

namespace MobileMarket.Environment
{
    /// <summary>
    /// Moves a door's leaves between closed and open: each leaf swings on its hinge pivot (openEuler) and/or slides
    /// (openOffset), in its parent's local space. Shutters and key doors call Open(); doors left open in the map start open.
    /// </summary>
    public class DoorLeafMotion : MonoBehaviour
    {
        [Serializable]
        private class Leaf
        {
            [Tooltip("Hinge pivot (or the leaf itself for sliding leaves).")]
            public Transform pivot;
            public Vector3 openOffset;
            public Vector3 openEuler;
            [Tooltip("Scale multiplier when open (a roller shutter rolls up: y < 1).")]
            public Vector3 openScale = Vector3.one;
        }

        [SerializeField] private Leaf[] leaves = Array.Empty<Leaf>();
        [SerializeField] private float openTime = 1.5f;
        [Tooltip("Shown open from the start (passable doorways).")]
        [SerializeField] private bool startOpen;

        private Vector3[] closedPositions;
        private Quaternion[] closedRotations;
        private Vector3[] closedScales;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            closedPositions = new Vector3[leaves.Length];
            closedRotations = new Quaternion[leaves.Length];
            closedScales = new Vector3[leaves.Length];
            for (int i = 0; i < leaves.Length; i++)
            {
                closedPositions[i] = leaves[i].pivot.localPosition;
                closedRotations[i] = leaves[i].pivot.localRotation;
                closedScales[i] = leaves[i].pivot.localScale;
            }
            if (startOpen)
            {
                IsOpen = true;
                Apply(1f);
            }
        }

        [ContextMenu("Open (test)")]
        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            if (isActiveAndEnabled) StartCoroutine(Animate());
            else Apply(1f);
        }

        private IEnumerator Animate()
        {
            for (float t = 0f; t < openTime; t += Time.deltaTime)
            {
                Apply(Mathf.SmoothStep(0f, 1f, t / openTime));
                yield return null;
            }
            Apply(1f);
        }

        private void Apply(float k)
        {
            for (int i = 0; i < leaves.Length; i++)
            {
                leaves[i].pivot.localPosition = closedPositions[i] + leaves[i].openOffset * k;
                leaves[i].pivot.localRotation = closedRotations[i] * Quaternion.Euler(leaves[i].openEuler * k);
                leaves[i].pivot.localScale = Vector3.Scale(closedScales[i], Vector3.Lerp(Vector3.one, leaves[i].openScale, k));
            }
        }
    }
}
