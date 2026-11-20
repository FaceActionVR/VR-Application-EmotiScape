using UnityEngine;

namespace DeveloperTools
{
    public class WristMenuPresenter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DeveloperModeConfig config;
        [SerializeField] private Transform menuRoot;

        [Header("Placement")]
        [SerializeField] private bool useLeftController = true;
        [SerializeField] private Vector3 controllerOffset = new Vector3(0.10f, 0.08f, 0.12f);
        [SerializeField] private float fallbackDistanceFromCamera = 1.0f;
        [SerializeField] private bool faceCamera = true;

        [Header("State")]
        [SerializeField] private bool isVisible;

        private bool controllerWarningShown;

        public bool IsVisible => isVisible;

        private void Awake()
        {
            ResolveReferences();
            SetVisible(false);
        }

        private void Update()
        {
            if (config != null && !config.DebugMode)
            {
                SetVisible(false);
                return;
            }

            if (isVisible)
                UpdateTransform();
        }

        private void ResolveReferences()
        {
            if (config == null)
                config = GetComponentInParent<DeveloperModeConfig>();

            if (menuRoot == null)
                menuRoot = transform;
        }

        public void Toggle()
        {
            SetVisible(!isVisible);
        }

        public void SetVisible(bool visible)
        {
            isVisible = visible;

            Debug.Log(
                $"[WristMenuPresenter] Menu visibility changed: {visible}");

            if (menuRoot != null)
            {
                menuRoot.gameObject.SetActive(visible);
                Debug.Log(
                    $"[WristMenuPresenter] MenuRoot active: {menuRoot.gameObject.activeSelf}");
            }
            else
            {
                Debug.LogWarning(
                    "[WristMenuPresenter] MenuRoot is NULL.");
            }
        }
        private void UpdateTransform()
        {
            var resolver = SceneRigResolver.Instance;

            Transform camera = resolver != null ? resolver.CameraTransform : null;
            Transform controller = null;

            if (resolver != null)
            {
                controller = useLeftController
                    ? resolver.LeftControllerTransform
                    : resolver.RightControllerTransform;
            }

            if (controller == null)
            {
                if (!controllerWarningShown)
                {
                    controllerWarningShown = true;
                    Debug.LogWarning(
                        "[WistMenuPresenter] Controller not found. Using camera fallback.");
                }

            }
            else
            {
                controllerWarningShown = false;
            }

            if (controller != null)
            {
                menuRoot.position = controller.TransformPoint(controllerOffset);
            }
            else if (camera != null)
            {
                menuRoot.position = camera.position + camera.forward * fallbackDistanceFromCamera;
            }

            if (faceCamera && camera != null)
            {
                Vector3 direction = menuRoot.position - camera.position;

                if (direction.sqrMagnitude > 0.001f)
                    menuRoot.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
        }
    }
}