using UnityEngine;

namespace DeveloperTools
{
    public class SceneRigReferenceProvider : MonoBehaviour
    {
        [Header("Scene XR Rig")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private Transform leftControllerTransform;
        [SerializeField] private Transform rightControllerTransform;

        public Transform CameraTransform => cameraTransform;
        public Transform LeftControllerTransform => leftControllerTransform;
        public Transform RightControllerTransform => rightControllerTransform;

        private void OnEnable()
        {
            Register();
        }
        private void Start()
        {
            Register();
        }

        private void OnDisable()
        {
            if (SceneRigResolver.Instance != null)
            {
                SceneRigResolver.Instance.ClearRig(this);
            }
        }

        private void Register()
        {
            if (SceneRigResolver.Instance == null)
            {
                Debug.LogWarning("[SceneRigReferenceProvider] SceneRigResolver not found.");
                return;
            }

            Debug.Log(
                $"[SceneRigReferenceProvider] Registering rig. " +
                $"Camera: {(cameraTransform != null ? cameraTransform.name : "NULL")}, " +
                $"Left: {(leftControllerTransform != null ? leftControllerTransform.name : "NULL")}, " +
                $"Right: {(rightControllerTransform != null ? rightControllerTransform.name : "NULL")}"
            );

            SceneRigResolver.Instance.RegisterRig(
                cameraTransform,
                leftControllerTransform,
                rightControllerTransform);
        }
    }
}