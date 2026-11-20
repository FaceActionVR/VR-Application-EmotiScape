using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

namespace DeveloperTools
{
    public class DevMenuInputController : MonoBehaviour
    {
        private const string LogPrefix = "[DEV_MENU_INPUT]";

        private enum ControllerFacingAxis
        {
            Up,
            Down,
            Forward,
            Back,
            Right,
            Left
        }

        [Header("References")]
        [SerializeField] private DeveloperModeConfig config;
        [SerializeField] private WristMenuPresenter presenter;

        [Header("Debug")]
        [SerializeField] private bool isDebug = true;
        [SerializeField] private float debugLogInterval = 0.5f;

        [Header("Keyboard / Editor Test")]
        [SerializeField] private bool enableKeyboardToggle = true;
        [SerializeField] private Key toggleKey = Key.M;

        [Header("Controller Activation")]
        [Tooltip("Reader inputu aktywacji menu. Dla gripa ustaw XRI Left Interaction / Select Value.")]
        [SerializeField]
        private XRInputValueReader<float> activationInput = new XRInputValueReader<float>("Dev Menu Activation");

        [Tooltip("Próg wartoœci analogowej inputu. Dla grip/trigger zwykle 0.5.")]
        [Range(0f, 1f)]
        [SerializeField] private float activationThreshold = 0.5f;

        [Tooltip("Ile sekund trzeba przytrzymaæ input, ¿eby menu siê otworzy³o.")]
        [SerializeField] private float holdToShowSeconds = 0.5f;

        [Tooltip("Czy wymagamy skierowania kontrolera do kamery.")]
        [SerializeField] private bool requirePalmFacingCamera = true;

        [Tooltip("Próg dot product. Wy¿szy = bardziej precyzyjne ustawienie kontrolera.")]
        [Range(-1f, 1f)]
        [SerializeField] private float palmFacingThreshold = 0.25f;

        [Tooltip("Który lokalny kierunek kontrolera traktujemy jako stronê skierowan¹ do twarzy.")]
        [SerializeField] private ControllerFacingAxis palmFacingAxis = ControllerFacingAxis.Up;

        [Tooltip("U¿yj lewego kontrolera. Jeœli false, u¿yje prawego.")]
        [SerializeField] private bool useLeftController = true;

        [Tooltip("Jeœli true, zaakceptowany gest prze³¹cza menu on/off. Jeœli false, tylko pokazuje menu.")]
        [SerializeField] private bool toggleMenuOnGesture = true;

        [Header("Runtime State")]
        [SerializeField] private bool hasController;
        [SerializeField] private bool hasCamera;
        [SerializeField] private float activationValue;
        [SerializeField] private bool activationHeld;
        [SerializeField] private float palmFacingDot;
        [SerializeField] private bool palmFacingCamera;
        [SerializeField] private float holdTimer;
        [SerializeField] private bool gestureConsumedUntilRelease;

        private float nextDebugLogTime;

        private void Awake()
        {
            ResolveReferences();

            Log(
                $"Awake. " +
                $"Config={(config != null ? config.name : "NULL")}, " +
                $"Presenter={(presenter != null ? presenter.name : "NULL")}");
        }

        private void OnEnable()
        {
            activationInput?.EnableDirectActionIfModeUsed();

            Log(
                $"OnEnable. Activation input enabled. " +
                $"InputName={(activationInput != null ? "Assigned" : "NULL")}");
        }

        private void OnDisable()
        {
            activationInput?.DisableDirectActionIfModeUsed();

            Log("OnDisable. Activation input disabled.");
        }

        private void Update()
        {
            ResolveReferences();

            if (config != null && !config.DebugMode)
            {
                if (presenter != null && presenter.IsVisible)
                    presenter.SetVisible(false);

                ResetHold("DebugMode disabled.");
                return;
            }

            HandleKeyboardInput();
            HandleControllerGestureInput();
            PeriodicDebugLog();
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

            if (!Keyboard.current[toggleKey].wasPressedThisFrame)
                return;

            Log($"Keyboard toggle pressed: {toggleKey}");

            if (presenter == null)
            {
                LogWarning("Presenter is NULL. Cannot toggle menu.");
                return;
            }

            presenter.Toggle();

            Log($"Menu toggled by keyboard. Visible={presenter.IsVisible}");
        }

        private void HandleControllerGestureInput()
        {
            Transform controller = GetControllerTransform();
            Transform camera = GetCameraTransform();

            hasController = controller != null;
            hasCamera = camera != null;

            if (!hasController || !hasCamera)
            {
                ResetHold(
                    $"Missing references. " +
                    $"Controller={(controller != null ? controller.name : "NULL")}, " +
                    $"Camera={(camera != null ? camera.name : "NULL")}");

                return;
            }

            activationValue = ReadActivationValue();
            activationHeld = activationValue >= activationThreshold;

            palmFacingDot = CalculatePalmFacingDot(controller, camera);
            palmFacingCamera = palmFacingDot >= palmFacingThreshold;

            if (!activationHeld)
            {
                gestureConsumedUntilRelease = false;
                ResetHold($"Activation not held. Value={activationValue:F3}");
                return;
            }

            if (requirePalmFacingCamera && !palmFacingCamera)
            {
                ResetHold(
                    $"Palm not facing camera. " +
                    $"Dot={palmFacingDot:F3}, Threshold={palmFacingThreshold:F3}");

                return;
            }

            if (gestureConsumedUntilRelease)
                return;

            holdTimer += Time.deltaTime;

            if (holdTimer < holdToShowSeconds)
                return;

            AcceptGesture();
        }

        private void AcceptGesture()
        {
            if (presenter == null)
            {
                LogWarning("Presenter is NULL. Cannot show/toggle menu.");
                return;
            }

            gestureConsumedUntilRelease = true;

            if (toggleMenuOnGesture)
            {
                presenter.Toggle();

                Log(
                    $"Gesture accepted. Menu toggled. " +
                    $"Visible={presenter.IsVisible}, " +
                    $"HoldTimer={holdTimer:F3}, " +
                    $"ActivationValue={activationValue:F3}, " +
                    $"PalmDot={palmFacingDot:F3}");
            }
            else
            {
                presenter.SetVisible(true);

                Log(
                    $"Gesture accepted. Menu shown. " +
                    $"HoldTimer={holdTimer:F3}, " +
                    $"ActivationValue={activationValue:F3}, " +
                    $"PalmDot={palmFacingDot:F3}");
            }

            holdTimer = 0f;
        }

        private Transform GetControllerTransform()
        {
            SceneRigResolver resolver = SceneRigResolver.Instance;

            if (resolver == null)
                return null;

            return useLeftController
                ? resolver.LeftControllerTransform
                : resolver.RightControllerTransform;
        }

        private Transform GetCameraTransform()
        {
            SceneRigResolver resolver = SceneRigResolver.Instance;

            if (resolver == null)
                return null;

            return resolver.CameraTransform;
        }

        private float ReadActivationValue()
        {
            if (activationInput == null)
                return 0f;

            try
            {
                return activationInput.ReadValue();
            }
            catch (System.Exception e)
            {
                LogWarning($"Failed to read activation input: {e.Message}");
                return 0f;
            }
        }

        private float CalculatePalmFacingDot(
            Transform controller,
            Transform camera)
        {
            Vector3 toCamera =
                (camera.position - controller.position).normalized;

            Vector3 facingDirection =
                GetControllerAxisDirection(controller, palmFacingAxis);

            return Vector3.Dot(
                facingDirection,
                toCamera);
        }

        private Vector3 GetControllerAxisDirection(
            Transform controller,
            ControllerFacingAxis axis)
        {
            switch (axis)
            {
                case ControllerFacingAxis.Up:
                    return controller.up;

                case ControllerFacingAxis.Down:
                    return -controller.up;

                case ControllerFacingAxis.Forward:
                    return controller.forward;

                case ControllerFacingAxis.Back:
                    return -controller.forward;

                case ControllerFacingAxis.Right:
                    return controller.right;

                case ControllerFacingAxis.Left:
                    return -controller.right;

                default:
                    return controller.up;
            }
        }

        private void ResetHold(string reason)
        {
            if (holdTimer > 0f)
            {
                Log(
                    $"Hold reset. Reason: {reason} " +
                    $"PreviousHold={holdTimer:F3}");
            }

            holdTimer = 0f;
        }

        private void PeriodicDebugLog()
        {
            if (!isDebug)
                return;

            if (Time.time < nextDebugLogTime)
                return;

            nextDebugLogTime = Time.time + debugLogInterval;

            Transform controller = GetControllerTransform();
            Transform camera = GetCameraTransform();

            Log(
                $"State | " +
                $"DebugMode={(config != null && config.DebugMode)} | " +
                $"Presenter={(presenter != null ? presenter.name : "NULL")} | " +
                $"Visible={(presenter != null && presenter.IsVisible)} | " +
                $"Controller={(controller != null ? controller.name : "NULL")} | " +
                $"Camera={(camera != null ? camera.name : "NULL")} | " +
                $"ActivationInput={(activationInput != null ? "Assigned" : "NULL")} | " +
                $"ActivationValue={activationValue:F3} | " +
                $"Held={activationHeld} | " +
                $"PalmAxis={palmFacingAxis} | " +
                $"PalmDot={palmFacingDot:F3} | " +
                $"PalmOk={palmFacingCamera} | " +
                $"HoldTimer={holdTimer:F3} | " +
                $"Consumed={gestureConsumedUntilRelease}");
        }

        private void Log(string message)
        {
            if (!isDebug)
                return;

            Debug.Log($"{LogPrefix} {message}");
        }

        private void LogWarning(string message)
        {
            if (!isDebug)
                return;

            Debug.LogWarning($"{LogPrefix} {message}");
        }
    }
}