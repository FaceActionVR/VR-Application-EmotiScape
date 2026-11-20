using System;
using System.Collections.Generic;
using Telemetry.DataReaders;
using UnityEngine;
using VIVE.OpenXR.FacialTracking;

public enum SamplingMode
{
    FPS,        // X próbek na sekundę
    Interval    // próbka co Y sekund
}

public class ExpressionChangeRecorder : MonoBehaviour
{
    // --- EVENT dla systemu zapisu ---
    // Inne skrypty mogą się tu podpiąć i słuchać zmian
    public event Action<ChangeEvent> OnChangeEvent;
    [Tooltip("Wyłączone ponieważ jest to stary typ, wykrywający zmiany twarzy")]

    [Header("Sampling")]
    public SamplingMode samplingMode = SamplingMode.FPS;

    [Tooltip("Ile razy na sekundę próbkujemy (gdy SamplingMode = FPS).")]
    public float samplesPerSecond = 20f;

    [Tooltip("Co ile sekund pobieramy próbkę (gdy SamplingMode = Interval).")]
    public float intervalSeconds = 0.05f;

    [Tooltip("Minimalna zmiana (0-1) wymagana do zalogowania nowej próbki twarzy.")]
    [SerializeField] private float lipChangeThreshold = 0.01f;
    [Tooltip("Minimalna zmiana (0-1) wymagana do zalogowania nowej próbki oczu.")]
    [SerializeField] private float eyeChangeThreshold = 0.05f;

    private float samplingInterval;
    private float timer;

    private float[] prevEyes;
    private float[] prevLips;
    private bool initialized;

    // Jedno „zdarzenie zmiany”
    [Serializable]
    public struct ChangeEvent
    {
        public float time;      // Time.time
        public bool isEye;      // true = eye, false = lip
        public int index;       // indeks w tablicy
        public float value;     // nowa wartość
    }

    void Start()
    {
        UpdateSamplingInterval();
        TryInitialize();
    }

    /// <summary>
    /// Próbuje pobrać referencje do tablic z FaceReader.
    /// Zwraca true, jeśli się udało.
    /// </summary>
    private bool TryInitialize()
    {
        // Pobieramy dane z Twojego drugiego skryptu
        var eyes = FaceReader.Eyes;
        var lips = FaceReader.Lips;

        // Jeśli FaceReader jeszcze nie wstał (jest null), przerywamy i czekamy.
        if (eyes == null || lips == null)
        {
            Debug.LogWarning("[ExpressionChangeRecorder] FaceReader zwrócił null w czasie działania.");
            initialized = false;
            return false;
        }

        // Inicjalizacja buforów poprzedniego stanu
        prevEyes = new float[eyes.Length];
        prevLips = new float[lips.Length];

        Array.Copy(eyes, prevEyes, eyes.Length);
        Array.Copy(lips, prevLips, lips.Length);

        initialized = true;
        Debug.Log("[ExpressionChangeRecorder] Pomyślnie zainicjowano połączenie z FaceReader.");
        return true;
    }

    void OnValidate() => UpdateSamplingInterval();

    private void UpdateSamplingInterval()
    {
        if (samplingMode == SamplingMode.FPS)
        {
            // FPS → interwał to 1 / FPS
            samplingInterval = (samplesPerSecond > 0f) ? 1f / samplesPerSecond : 0.05f;//domyslnie ~20fps
        }
        else
        {
            // Interval → interwał to wartość w sekundach
            samplingInterval = Mathf.Max(0f, intervalSeconds);
        }
    }


    void Update()
    {
        // 1. MECHANIZM RETRY: Jeśli nie zainicjowano, próbuj do skutku
        if (!initialized)
        {
            if (!TryInitialize())
            {
                return; // Wciąż brak danych, czekamy do następnej klatki
            }
        }

        // 2. LOGIKA TIMERA
        timer += Time.deltaTime;
        if (samplingInterval > 0f && timer < samplingInterval)
        {
            return;
        }
        timer = 0f;

        // 3. OD CZYT DANYCH
        var eyes = FaceReader.Eyes;
        var lips = FaceReader.Lips;

        // Zabezpieczenie na wypadek, gdyby tablice zmieniły rozmiar (mało prawdopodobne, ale bezpieczne)
        if (eyes == null || lips == null || eyes.Length != prevEyes.Length || lips.Length != prevLips.Length)
        {
            initialized = false; // Wymuś reinstalację
            return;
        }

        float t = Time.time;


        // --- Analiza Oczu ---
        CheckChanges(eyes, prevEyes, true, eyeChangeThreshold, t);
        // --- Analiza Ust ---
        CheckChanges(lips, prevLips, false, lipChangeThreshold, t);
    }

    private void CheckChanges(float[] current, float[] previous, bool isEye, float threshold, float time)
    {
        for (int i = 0; i < current.Length; i++)
        {
            float curr = current[i];
            float diff = Mathf.Abs(curr - previous[i]);

            if (diff >= threshold)
            {
                // Aktualizacja poprzedniego stanu
                previous[i] = curr;

                // WYWOŁANIE EVENTU -> Wyślij dane do każdego kto słucha (np. Writera)
                OnChangeEvent?.Invoke(new ChangeEvent
                {
                    time = time,
                    isEye = isEye,
                    index = i,
                    value = curr
                });
            }
        }
    }


    // --- EKSPORT DANYCH ---

    /// <summary>
    /// Pomocnicza metoda zamieniająca indeks na nazwę z SDK
    /// </summary>
   // --- Metoda pomocnicza dostępna statycznie dla Writera ---
    public static string GetExpressionName(bool isEye, int index)
    {
        if (isEye)
            return Enum.IsDefined(typeof(XrEyeExpressionHTC), index) ? ((XrEyeExpressionHTC)index).ToString() : index.ToString();
        else
            return Enum.IsDefined(typeof(XrLipExpressionHTC), index) ? ((XrLipExpressionHTC)index).ToString() : index.ToString();
    }
}
