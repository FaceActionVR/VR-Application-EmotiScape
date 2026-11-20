using UnityEngine;

namespace DeveloperTools
{

    public class SceneRigResolver : MonoBehaviour
    {
        public static SceneRigResolver Instance { get; private set; }

        public Transform CameraTransform { get; private set; }
        public Transform LeftControllerTransform { get; private set; }
        public Transform RightControllerTransform { get; private set; }

        public bool HasCamera => CameraTransform != null;
        public bool HasLeftController => LeftControllerTransform != null;
        public bool HasRightController => RightControllerTransform != null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public void RegisterRig(
            Transform cameraTransform,
            Transform leftControllerTransform,
            Transform rightControllerTransform)
        {
            CameraTransform = cameraTransform;
            LeftControllerTransform = leftControllerTransform;
            RightControllerTransform = rightControllerTransform;

            Debug.Log("[SceneRigResolver] Scene rig registered.");
        }

        public void ClearRig(SceneRigReferenceProvider provider)
        {
            if (provider == null)
                return;

            if (CameraTransform == provider.CameraTransform)
                CameraTransform = null;

            if (LeftControllerTransform == provider.LeftControllerTransform)
                LeftControllerTransform = null;

            if (RightControllerTransform == provider.RightControllerTransform)
                RightControllerTransform = null;

            Debug.Log("[SceneRigResolver] Scene rig cleared.");
        }
    }
}