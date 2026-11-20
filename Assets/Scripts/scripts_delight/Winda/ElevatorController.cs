using System.Collections;
using Telemetry.Interactions;
using UnityEngine;
using Unity.XR.CoreUtils;


public class ElevatorController : MonoBehaviour
{
    [Header("Elevator")]
    [SerializeField] private Rigidbody elevatorRigidbody;

    [Header("Doors")]
    [SerializeField] private Animator doorAnimator;

    [Tooltip("Ile sekund trwa zamykanie drzwi.")]
    [SerializeField] private float doorCloseTime = 3f;

    [Header("Player")]
    [SerializeField] private ElevatorCabinTrigger cabinTrigger;

    [Header("Door Audio")]
    [SerializeField] private AudioSource doorAudioSource;

    [SerializeField]
    private AudioClip doorOpenSound;

    [SerializeField]
    private AudioClip doorCloseSound;

    [Header("Telemetry")]
    [SerializeField]
    private InteractionEventLogger interactionLogger;

    [Header("Movement")]

    [Tooltip("Prêdkoœæ p³ynnej jazdy windy w metrach na sekundê.")]
    [SerializeField] private float moveSpeed = 1.5f;

    [Tooltip("O ile metrów winda ma zjechaæ w dó³ od pozycji pocz¹tkowej.")]
    [SerializeField] private float travelDistance = 6f;

    private Vector3 topPosition;
    private Vector3 bottomPosition;

    private bool isMoving;
    private bool keepDoorsOpen;

    private XROrigin passenger;


    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        if (elevatorRigidbody == null)
        {
            Debug.LogError("ElevatorController: Brakuje Rigidbody windy!");
            return;
        }

        // Pozycja, w której winda znajduje siê na pocz¹tku gry,
        // jest traktowana jako GÓRNE piêtro.
        topPosition = elevatorRigidbody.position;

        // Dolne piêtro znajduje siê travelDistance metrów ni¿ej.
        bottomPosition =
            topPosition + Vector3.down * travelDistance;

        Debug.Log(
            "Winda: góra Y = " + topPosition.y +
            " | dó³ Y = " + bottomPosition.y
        );
    }

    // =========================================================
    // PRZYCISK GO
    // =========================================================

    public void GoSmooth()
    {
        if (!CanTravel())
            return;

        ResolveTelemetry();

        if (interactionLogger != null)
        {
            interactionLogger.LogPointEvent(
                "ELEVATOR_ACTIVATED",
                "Elevator",
                "{\"mode\":\"smooth\"}"
            );
        }

        StartCoroutine(Travel(false));
    }

    private void ResolveTelemetry()
    {
        if (interactionLogger == null)
        {
            interactionLogger =
                FindObjectOfType<InteractionEventLogger>();
        }
    }

    // =========================================================
    // PRZYCISK TELEPORT
    // =========================================================

    public void GoTeleport()
    {
        if (!CanTravel())
            return;

        ResolveTelemetry();

        if (interactionLogger != null)
        {
            interactionLogger.LogPointEvent(
                "ELEVATOR_ACTIVATED",
                "Elevator",
                "{\"mode\":\"teleport\"}"
            );
        }

        StartCoroutine(Travel(true));
    }

    // =========================================================
    // CZY WINDA MO¯E JECHAÆ
    // =========================================================

    private bool CanTravel()
    {
        if (isMoving)
        {
            Debug.Log("Winda ju¿ jedzie.");
            return false;
        }

        if (elevatorRigidbody == null)
        {
            Debug.LogError("Brak Rigidbody windy!");
            return false;
        }

        if (doorAnimator == null)
        {
            Debug.LogError("Brak Animatora drzwi!");
            return false;
        }

        if (cabinTrigger == null)
        {
            Debug.LogError("Brak CabinTrigger!");
            return false;
        }

        if (!cabinTrigger.PlayerInside)
        {
            Debug.Log(
                "Nie mo¿na uruchomiæ windy - gracza nie ma w kabinie."
            );

            return false;
        }

        if (cabinTrigger.Player == null)
            return false;

        return true;
    }

    // =========================================================
    // PODRÓ¯
    // =========================================================

    private IEnumerator Travel(bool teleport)
    {
        isMoving = true;

        // Zapamiêtujemy gracza znajduj¹cego siê w windzie.
        passenger = cabinTrigger.Player;

        // Od momentu klikniêcia przycisku drzwi mog¹ siê zamkn¹æ.
        keepDoorsOpen = false;
        SetDoorsOpen(false);
      

        // Poczekaj a¿ animacja zamykania siê zakoñczy.
        yield return new WaitForSeconds(doorCloseTime);

        // SprawdŸ gdzie faktycznie znajduje siê winda
        // i wybierz przeciwne piêtro.
        Vector3 destination = GetOppositeFloor();

        // TELEPORT albo normalna jazda.
        if (teleport)
        {
            TeleportElevator(destination);
        }
        else
        {
            yield return MoveElevator(destination);
        }

        // Ma³a pauza po dotarciu.
        yield return new WaitForSeconds(0.15f);

        // Otwórz drzwi.
        SetDoorsOpen(true);

        // Drzwi pozostaj¹ otwarte po przyjeŸdzie.
        keepDoorsOpen = true;

        passenger = null;
        isMoving = false;
    }

    // =========================================================
    // WYBIERANIE PRZECIWNEGO PIÊTRA
    // =========================================================

    private Vector3 GetOppositeFloor()
    {
        Vector3 currentPosition =
            elevatorRigidbody.position;

        float distanceToTop =
            Vector3.Distance(
                currentPosition,
                topPosition
            );

        float distanceToBottom =
            Vector3.Distance(
                currentPosition,
                bottomPosition
            );

        // Je¿eli bli¿ej nam do góry,
        // jedziemy na dó³.
        if (distanceToTop < distanceToBottom)
        {
            Debug.Log("Winda jest na GÓRZE -> jedzie na DÓ£");

            return bottomPosition;
        }

        // Je¿eli bli¿ej nam do do³u,
        // jedziemy na górê.
        Debug.Log("Winda jest na DOLE -> jedzie na GÓRÊ");

        return topPosition;
    }

    // =========================================================
    // P£YNNA JAZDA
    // =========================================================

    private IEnumerator MoveElevator(Vector3 destination)
    {
        while (
            Vector3.Distance(
                elevatorRigidbody.position,
                destination
            ) > 0.005f
        )
        {
            yield return new WaitForFixedUpdate();

            Vector3 oldPosition =
                elevatorRigidbody.position;

            Vector3 newPosition =
                Vector3.MoveTowards(
                    oldPosition,
                    destination,
                    moveSpeed * Time.fixedDeltaTime
                );

            Vector3 movement =
                newPosition - oldPosition;

            // Przesuwamy windê.
            elevatorRigidbody.MovePosition(newPosition);

            // Przesuwamy gracza razem z wind¹.
            MovePassenger(movement);
        }

        // Dok³adne ustawienie na koñcowej pozycji.
        Vector3 finalMovement =
            destination - elevatorRigidbody.position;

        elevatorRigidbody.position = destination;

        MovePassenger(finalMovement);
    }

    // =========================================================
    // TELEPORT WINDY
    // =========================================================

    private void TeleportElevator(Vector3 destination)
    {
        Vector3 oldPosition =
            elevatorRigidbody.position;

        Vector3 movement =
            destination - oldPosition;

        // Teleport windy.
        elevatorRigidbody.position = destination;

        // Teleport gracza o dok³adnie ten sam dystans.
        TeleportPassenger(movement);
    }

    // =========================================================
    // GRACZ PODCZAS NORMALNEJ JAZDY
    // =========================================================

    private void MovePassenger(Vector3 movement)
    {
        if (passenger == null)
            return;

        CharacterController controller =
            passenger.GetComponent<CharacterController>();

        if (controller != null && controller.enabled)
        {
            controller.Move(movement);
        }
        else
        {
            passenger.transform.position += movement;
        }
    }

    // =========================================================
    // GRACZ PODCZAS TELEPORTU
    // =========================================================

    private void TeleportPassenger(Vector3 movement)
    {
        if (passenger == null)
            return;

        CharacterController controller =
            passenger.GetComponent<CharacterController>();

        if (controller != null)
        {
            bool wasEnabled =
                controller.enabled;

            // Na chwilê wy³¹czamy CharacterController,
            // aby fizyka nie blokowa³a teleportu.
            controller.enabled = false;

            passenger.transform.position += movement;

            controller.enabled = wasEnabled;
        }
        else
        {
            passenger.transform.position += movement;
        }
    }

    // =========================================================
    // OTWIERANIE DRZWI PRZEZ TRIGGER
    // =========================================================

    public void OpenDoorsFromProximity()
    {
        if (isMoving)
            return;

        SetDoorsOpen(true);
    }

    // =========================================================
    // ZAMYKANIE DRZWI PRZEZ TRIGGER
    // =========================================================

    public void CloseDoorsFromProximity()
    {
        if (isMoving)
            return;

        // Po dojechaniu drzwi maj¹ pozostaæ otwarte.
        if (keepDoorsOpen)
            return;

        // Je¿eli gracz znajduje siê w kabinie,
        // drzwi równie¿ pozostaj¹ otwarte.
        // Zamkn¹ siê dopiero po klikniêciu GO / TELEPORT.
        if (
            cabinTrigger != null &&
            cabinTrigger.PlayerInside
        )
        {
            return;
        }

        SetDoorsOpen(false);
    }

    private void SetDoorsOpen(bool open)
    {
        if(doorAnimator == null)
        {
            return;
        }

        bool currentlyOpen = doorAnimator.GetBool("Open");

        if (currentlyOpen == open)
        {
            return;
        }
        
        doorAnimator.SetBool("Open", open);
        PlayDoorSound(open);
    }

    private void PlayDoorSound(bool opening)
    {
        if(doorAudioSource == null)
        {
            return;
        }

        AudioClip clip;

        if (opening){
            clip = doorOpenSound;
        }
        else
        {
            clip = doorCloseSound;
        }

        if (clip == null)
        {
            return;
        }

        doorAudioSource.Stop();

        doorAudioSource.clip = clip;
        doorAudioSource.Play();
    }
}