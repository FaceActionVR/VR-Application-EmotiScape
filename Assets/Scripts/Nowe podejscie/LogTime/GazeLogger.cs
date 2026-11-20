using System.IO;
using UnityEngine;
using System;

public class GazeLogger : MonoBehaviour
{
    [Header("Ustawienia")]
    [Tooltip("Prefiks nazwy pliku, np. 'Sesja_Pacjent1'")]
    public string fileNamePrefix = "GazeLog";

    [Header("Debug (Tylko do podgl¹du)")]
    [SerializeField] private string currentFilePath;
    [SerializeField] private bool isLogging = false;

    private StreamWriter writer;
    private string filePath;
    void OnEnable()
    {
        StartLogging();
    }

    void OnDisable()
    {
        StopLogging();
    }

    private void StartLogging()
    {
        if (isLogging) return;

        // Generujemy nazwê z dat¹, ¿eby nie nadpisywaæ starych sesji
        string fileName = $"{fileNamePrefix}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv";
        currentFilePath = Path.Combine(Application.persistentDataPath, fileName);

        try
        {
            // 'true' w StreamWriter oznacza tryb "Append" (dopisywanie)
            writer = new StreamWriter(currentFilePath, true);

            // Piszemy nag³ówek kolumn
            writer.WriteLine("Timestamp_Enter;Timestamp_Exit;TargetID;Duration_Seconds;RoomID");

            // Flush wymusza zapis z RAMu na dysk
            writer.Flush();

            isLogging = true;
            Debug.Log($"[GazeLogger] Rozpoczêto zapis do: {currentFilePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[GazeLogger] B£¥D KRYTYCZNY - Nie mo¿na utworzyæ pliku: {e.Message}");
            // Wy³¹czamy skrypt, ¿eby nie próbowa³ logowaæ dalej
            this.enabled = false;
        }
    }

    /// <summary>
    /// Wywo³ywane przez GazeDwellManager, gdy wzrok opuœci obiekt.
    /// </summary>
    public void LogGazeSession(string targetId, double enterTime, double exitTime, int roomId)
    {
        // Zabezpieczenie: jeœli logger nie dzia³a, po prostu wyjdŸ
        if (!isLogging || writer == null) return;

        double duration = exitTime - enterTime;

        // Format CSV: Wejœcie; Wyjœcie; Co; Jak d³ugo
        // F3 oznacza 3 miejsca po przecinku (np. 12.345)
        string line = $"{enterTime:F3};{exitTime:F3};{targetId};{duration:F3};{roomId}";

        try
        {
            writer.WriteLine(line);

            // Wa¿ne: Przy logowaniu zdarzeniowym (rzadkim) warto robiæ Flush od razu.
            // Dziêki temu przy crashu aplikacji nie stracisz ostatniej linii.
            writer.Flush();
        }
        catch (Exception e)
        {
            Debug.LogError($"[GazeLogger] B³¹d zapisu linii: {e.Message}");
        }
    }

    private void StopLogging()
    {
        if (!isLogging) return;

        try
        {
            if (writer != null)
            {
                writer.Close();
                writer.Dispose();
                writer = null;
            }
            Debug.Log($"[GazeLogger] Plik zamkniêty poprawnie.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[GazeLogger] B³¹d przy zamykaniu pliku: {e.Message}");
        }
        finally
        {
            isLogging = false;
        }
    }
}