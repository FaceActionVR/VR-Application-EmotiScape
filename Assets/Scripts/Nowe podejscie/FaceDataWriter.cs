using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

[RequireComponent(typeof(ExpressionChangeRecorder))]
public class FaceDataWriter : MonoBehaviour
{
    public enum FileFormat { CSV, JSON_Lines }

    [Header("Settings")]
    public FileFormat format = FileFormat.CSV;
    public string fileNamePrefix = "FaceTrackingSession";

    [Tooltip("Zapisz na dysk dopiero gdy uzbiera siê tyle linii (optymalizacja).")]
    public int bufferSize = 100;

    [Header("Debug")]
    [SerializeField] private string currentFilePath;
    [SerializeField] private bool isRecording = false;

    private ExpressionChangeRecorder recorder;
    private StringBuilder buffer;
    private int bufferCount = 0;
    private StreamWriter fileWriter;

    void Awake()
    {
        recorder = GetComponent<ExpressionChangeRecorder>();
        buffer = new StringBuilder();
    }

    void OnEnable()
    {
        // Start nagrywania przy w³¹czeniu obiektu/sceny
        StartRecording();
        recorder.OnChangeEvent += HandleExpressionChange;
    }

    void OnDisable()
    {
        // Stop przy wy³¹czeniu
        recorder.OnChangeEvent -= HandleExpressionChange;
        StopRecording();
    }

    private void StartRecording()
    {
        if (isRecording) return;

        string extension = format == FileFormat.CSV ? "csv" : "json";
        // Dodajemy datê, ¿eby nie nadpisywaæ plików
        string filename = $"{fileNamePrefix}_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}.{extension}";
        currentFilePath = Path.Combine(Application.persistentDataPath, filename);

        try
        {
            // 65536 bajtów = 64KB bufora strumienia
            fileWriter = new StreamWriter(currentFilePath, true, Encoding.UTF8, 65536);

            // Jeœli CSV, dodaj nag³ówek
            if (format == FileFormat.CSV)
            {
                fileWriter.WriteLine("Time;Type;ExpressionName;Value");
            }

            isRecording = true;
            Debug.Log($"[FaceDataWriter] Rozpoczêto zapis do: {currentFilePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[FaceDataWriter] B³¹d otwierania pliku: {e.Message}");
            enabled = false; // Wy³¹cz skrypt jeœli nie mo¿na pisaæ
        }
    }

    private void HandleExpressionChange(ExpressionChangeRecorder.ChangeEvent evt)
    {
        if (!isRecording) return;

        string line = "";
        string name = ExpressionChangeRecorder.GetExpressionName(evt.isEye, evt.index);

        if (format == FileFormat.CSV)
        {
            // Format: 12.456;Eye;BlinkLeft;0.8543
            line = $"{evt.time:F3};{(evt.isEye ? "Eye" : "Lip")};{name};{evt.value:F4}";
        }
        else // JSON Lines (NDJSON) - ka¿da linia to osobny obiekt JSON
        {
            // Rêczne tworzenie prostego JSONa jest szybsze ni¿ JsonUtility dla ka¿dej linii
            line = $"{{\"t\":{evt.time:F3},\"type\":\"{(evt.isEye ? "eye" : "lip")}\",\"name\":\"{name}\",\"val\":{evt.value:F4}}}";
        }

        buffer.AppendLine(line);
        bufferCount++;

        // Zrzut bufora na dysk, gdy siê zape³ni
        if (bufferCount >= bufferSize)
        {
            FlushBuffer();
        }
    }

    private void FlushBuffer()
    {
        if (fileWriter != null && buffer.Length > 0)
        {
            fileWriter.Write(buffer.ToString());
            buffer.Clear();
            bufferCount = 0;
        }
    }

    private void StopRecording()
    {
        if (!isRecording) return;

        // Zapisz resztki z bufora
        FlushBuffer();

        if (fileWriter != null)
        {
            fileWriter.Close();
            fileWriter.Dispose();
            fileWriter = null;
        }

        isRecording = false;
        Debug.Log($"[FaceDataWriter] Zakoñczono zapis. Plik: {currentFilePath}");
    }
}
