using UnityEngine;
using UnityEngine.XR.OpenXR;
using VIVE.OpenXR;
using VIVE.OpenXR.EyeTracker;

public class PupilDataReader : MonoBehaviour
{
    public float DebugDiameter;
    public Vector2 DebugPosition;

    private ViveEyeTracker _viveEyeFeature;

    void Start()
    {
        // 1. Próbujemy pobraæ oficjalny Feature z ustawieñ OpenXR
        _viveEyeFeature = OpenXRSettings.Instance.GetFeature<ViveEyeTracker>();

        if (_viveEyeFeature == null)
        {
            Debug.LogError("[PupilDataReader] KRYTYCZNE: Unity w ogóle nie widzi 'ViveEyeTracker' w konfiguracji OpenXR!");
        }
        else
        {
            Debug.Log($"[PupilDataReader] Feature znaleziony. Czy jest w³¹czony? test123 -> {_viveEyeFeature.enabled}");
        }

        if (!_viveEyeFeature.enabled)
        {
            Debug.LogWarning("[PupilDataReader] Feature by³ wy³¹czony! Próbujê wymusiæ start (Force Enable)...");
            _viveEyeFeature.enabled = true;
        }

        Debug.Log($"[PupilDataReader] Stan po próbie w³¹czenia: {_viveEyeFeature.enabled}");
    }

    void Update()
    {
        // Jeœli Feature nie jest w³¹czony, nie ma sensu pytaæ o dane
        if (_viveEyeFeature == null || !_viveEyeFeature.enabled) return;

        // Wywo³ujemy funkcjê. Zwraca TRUE (sukces) lub FALSE (b³¹d/brak uprawnieñ)
        if (XR_HTC_eye_tracker.Interop.GetEyePupilData(out XrSingleEyePupilDataHTC[] out_pupils))
        {
            Debug.Log("[PupilDataReader], mamy dane");
            // --- SUKCES: MAMY DANE ---

            // Pobieramy indeks prawego oka
            int rightIndex = (int)XrEyePositionHTC.XR_EYE_POSITION_RIGHT_HTC;
            Debug.Log("[PupilDataReader], mamy rightIndex: " + rightIndex);
            // Zabezpieczenie: czy tablica jest wystarczaj¹co d³uga?
            if (out_pupils != null && rightIndex < out_pupils.Length)
            {

                var rightPupil = out_pupils[rightIndex];

                if (rightPupil.isDiameterValid)
                {
                    DebugDiameter = rightPupil.pupilDiameter;
                    Debug.Log($"[PupilDataReader][SUKCES] Srednica: {DebugDiameter}");
                }
                else
                {
                    Debug.Log($"[PupilDataReader][PORAZKA]: right Pupil is not DiameterValid");
                }

                if (rightPupil.isPositionValid)
                {
                    DebugPosition = new Vector2(rightPupil.pupilPosition.x, rightPupil.pupilPosition.y);
                }
            }
        }
        else
        {
            // --- B£¥D: SESSION LOST / BRAK UPRAWNIEÑ ---
            // Nie próbujemy czytaæ tablicy 'out_pupils', bo jest pusta!
            // To zapobiega b³êdom NullReferenceException.

            // Logujemy rzadziej, ¿eby nie spamowaæ
            if (Time.frameCount % 300 == 0) // Raz na 3-4 sekundy
            {
                Debug.LogWarning("[PupilDataReader]: Brak dostêpu do Eye Trackera (XR_ERROR_SESSION_LOST). SprawdŸ uprawnienia w goglach!");
            }
        }
    }
}