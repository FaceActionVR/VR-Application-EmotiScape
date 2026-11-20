using Telemetry.Interactions;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Telemetry.Interactions
{
    public class ObjectManipulationAnchor : MonoBehaviour
    {
        private const string LogPrefix = "[OBJECT_MANIPULATION_ANCHOR]";

        [Header("Object")]
        [Tooltip("Opcjonalny rêczny identyfikator. Jeœli puste, u¿ywana jest nazwa GameObjectu.")]
        [SerializeField] private string targetObjectId;

        [Header("XR")]
        [SerializeField] private XRBaseInteractable interactable;

        [Header("Telemetry")]
        [SerializeField] private InteractionEventLogger interactionLogger;

        [Header("Debug")]
        [SerializeField] private bool debugLogs = false;

        private int activeSelectCount = 0;
        private bool manipulationStarted = false;

        private void Awake()
        {
            ResolveReferences();

            if (string.IsNullOrWhiteSpace(targetObjectId))
            {
                targetObjectId = gameObject.name;

                Debug.LogWarning(
                    $"{LogPrefix} targetObjectId was empty on {gameObject.name}. " +
                    $"Using GameObject name: {targetObjectId}");
            }
        }

        private void OnEnable()
        {
            ResolveReferences();

            if (interactable == null)
                return;

            interactable.selectEntered.AddListener(OnSelectEntered);
            interactable.selectExited.AddListener(OnSelectExited);
        }

        private void OnDisable()
        {
            if (interactable != null)
            {
                interactable.selectEntered.RemoveListener(OnSelectEntered);
                interactable.selectExited.RemoveListener(OnSelectExited);
            }

            // Je¿eli obiekt znika razem ze scen¹ podczas trzymania,
            // zamykamy manipulacjê.
            if (manipulationStarted && interactionLogger != null)
            {
                interactionLogger.EndObjectManipulation(gameObject);
            }

            activeSelectCount = 0;
            manipulationStarted = false;
        }

        private void ResolveReferences()
        {
            if (interactable == null)
                interactable = GetComponent<XRBaseInteractable>();

            if (interactionLogger == null)
                interactionLogger =
                    FindObjectOfType<InteractionEventLogger>();
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            activeSelectCount++;

            // Pierwsza rêka/interaktor zaczyna manipulacjê.
            if (manipulationStarted)
                return;

            ResolveReferences();

            if (interactionLogger == null)
            {
                Debug.LogWarning(
                    $"{LogPrefix} InteractionEventLogger not found | " +
                    $"Object={gameObject.name}");
                return;
            }

            string resolvedTargetId = GetTargetObjectId();

            manipulationStarted = true;

            interactionLogger.BeginObjectManipulation(
                gameObject,
                resolvedTargetId
            );

            if (debugLogs)
            {
                Debug.Log(
                    $"{LogPrefix} BEGIN | Target={resolvedTargetId}");
            }
        }

        private void OnSelectExited(SelectExitEventArgs args)
        {
            activeSelectCount =
                Mathf.Max(0, activeSelectCount - 1);

            // Je¿eli np. obiekt jest trzymany dwoma interaktorami,
            // koñczymy dopiero po puszczeniu ostatniego.
            if (activeSelectCount > 0)
                return;

            if (!manipulationStarted)
                return;

            ResolveReferences();

            if (interactionLogger != null)
            {
                interactionLogger.EndObjectManipulation(gameObject);
            }

            manipulationStarted = false;

            if (debugLogs)
            {
                Debug.Log(
                    $"{LogPrefix} END | Target={GetTargetObjectId()}");
            }
        }

        private string GetTargetObjectId()
        {
            if (!string.IsNullOrWhiteSpace(targetObjectId))
                return targetObjectId;

            return gameObject.name;
        }
    }
}