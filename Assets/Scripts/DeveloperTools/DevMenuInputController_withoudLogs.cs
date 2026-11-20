using UnityEngine;
using UnityEngine.InputSystem;

namespace DeveloperTools
{
    public class DevMenuInputController_withoudLogs : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DeveloperModeConfig config;
        [SerializeField] private WristMenuPresenter presenter;

        [Header("Keyboard / Editor Test")]
        [SerializeField] private bool enableKeyboardToggle = true;
        [SerializeField] private Key toggleKey = Key.M;

        [Header("Controller Activation")]
        [Tooltip("InputAction od przycisku, który trzeba przytrzymaæ, np. left grip/menu/button.")]
        [SerializeField] private InputActionReference activationAction;

        [Tooltip("Ile sekund trzeba przytrzymaæ przycisk, ¿eby menu siê pojawi³o.")]
        [SerializeField] private float holdToShowSeconds = 0.5f;

        [Tooltip("Czy wymagamy, ¿eby kontroler by³ odwrócony w stronê twarzy.")]
        [SerializeField] private bool requirePalmFacingCamera = true;

        [Tooltip("Próg dot product. Im wy¿szy, tym precyzyjniej trzeba skierowaæ kontroler do twarzy.")]
        [Range(0f, 1f)]
        [SerializeField] private float palmFacingThreshold = 0.65f;

        [Tooltip("U¿yj lewego kontrolera. Jeœli false, u¿yje prawego.")]
        [SerializeField] private bool useLeftController = true;

        [Header("Debug")]
        [SerializeField] private float holdTimer;
        [SerializeField] private bool activationHeld;
        [SerializeField] private bool palmFacingCamera;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            if (activationAction != null)
                activationAction.action.Enable();
        }

        private void OnDisable()
        {
            if (activationAction != null)
                activationAction.action.Disable();
        }

        private void Update()
        {
            ResolveReferences();

            if (config != null && !config.DebugMode)
            {
                ResetHold();
                return;
            }

            HandleKeyboardInput();
            HandleControllerInput();
        }

        private void ResolveReferences()
        {
            if (config == null)
                config = GetComponentInParent<DeveloperModeConfig>();

            if (presenter == null)
                presenter = GetComponentInChildren<WristMenuPresenter>(true);
        }

        private void HandleKeyboardInput()
        {
            if (!enableKeyboardToggle)
                return;

            if (Keyboard.current == null)
                return;

            if (Keyboard.current[toggleKey].wasPressedThisFrame)
                presenter?.Toggle();
        }

        private void HandleControllerInput()
        {
            Transform controller = GetControllerTransform();
            Transform camera = SceneRigResolver.Instance != null
                ? SceneRigResolver.Instance.CameraTransform
                : null;

            if (controller == null || camera == null)
            {
                ResetHold();
                return;
            }

            palmFacingCamera = IsControllerPalmFacingCamera(controller, camera);
            activationHeld = IsActivationHeld();

            if (requirePalmFacingCamera && !palmFacingCamera)
            {
                ResetHold();
                return;
            }

            if (!activationHeld)
            {
                ResetHold();
                return;
            }

            holdTimer += Time.deltaTime;

            if (holdTimer >= holdToShowSeconds)
            {
                presenter?.SetVisible(true);
            }
        }

        private Transform GetControllerTransform()
        {
            var resolver = SceneRigResolver.Instance;

            if (resolver == null)
                return null;

            return useLeftController
                ? resolver.LeftControllerTransform
                : resolver.RightControllerTransform;
        }

        private bool IsActivationHeld()
        {
            if (activationAction == null)
                return false;

            if (activationAction.action == null)
                return false;

            return activationAction.action.IsPressed();
        }

        private bool IsControllerPalmFacingCamera(
            Transform controller,
            Transform camera)
        {
            Vector3 toCamera =
                (camera.position - controller.position).normalized;

            // Uwaga: zale¿y od orientacji modelu kontrolera.
            // Jeœli dzia³a odwrotnie, zmieñ na controller.up.
            Vector3 palmDirection =
                -controller.up;

            float dot =
                Vector3.Dot(palmDirection, toCamera);

            return dot >= palmFacingThreshold;
        }

        private void ResetHold()
        {
            holdTimer = 0f;
            activationHeld = false;
        }
    }
}