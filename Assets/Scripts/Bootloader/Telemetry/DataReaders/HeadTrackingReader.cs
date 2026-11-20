using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Telemetry.DataReaders
{
    public class HeadTrackingReader : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Transform g³owy / kamery XR. Najczêœciej XR Origin -> Camera Offset -> Main Camera.")]
        [SerializeField] private Transform headTransform;

        [Header("Fallback")]
        [Tooltip("Jeœli headTransform nie jest podpiêty, spróbuje u¿yæ Camera.main.")]
        [SerializeField] private bool fallbackToMainCamera = true;

        [Header("Debug")]
        [SerializeField] private bool hasValidTransform;
        [SerializeField] private Vector3 currentPosition;
        [SerializeField] private Quaternion currentRotation;

        public bool HasValidTransform => headTransform != null;

        public Vector3 CurrentPosition => headTransform != null
            ? headTransform.position
            : Vector3.zero;

        public Quaternion CurrentRotation => headTransform != null
            ? headTransform.rotation
            : Quaternion.identity;

        private void Awake()
        {
            ResolveReferences();
        }

        private void Update()
        {
            hasValidTransform = headTransform != null;

            if (!hasValidTransform)
            {
                ResolveReferences();
                return;
            }

            currentPosition = headTransform.position;
            currentRotation = headTransform.rotation;
        }

        private void ResolveReferences()
        {
            if (headTransform != null)
                return;

            if (!fallbackToMainCamera)
                return;

            if (Camera.main != null)
                headTransform = Camera.main.transform;
        }
    }
}