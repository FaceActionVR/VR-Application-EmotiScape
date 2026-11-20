using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class FileUploader : MonoBehaviour
{
    [Header("Server Settings")]
    // Wpisz IP swojego komputera w sieci lokalnej, np. "http://192.168.1.20:5000/upload"
    // domyœlnie to deviceflow.asuscomm.com
    public string serverUploadUrl = "http://192.168.50.209:5000/upload";

    /// <summary>
    /// Publiczna metoda do wywo³ania z innych skryptów po zakoñczeniu zapisu
    /// </summary>
    public void UploadFile(string filePath)
    {
        StartCoroutine(UploadCoroutine(filePath));
    }

    private IEnumerator UploadCoroutine(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError($"[FileUploader] Plik nie istnieje: {filePath}");
            yield break;
        }

        Debug.Log($"[FileUploader] Rozpoczynam wysy³anie: {Path.GetFileName(filePath)}");

        // Wczytujemy plik jako binarne dane
        byte[] fileData = File.ReadAllBytes(filePath);
        string fileName = Path.GetFileName(filePath);

        // Tworzymy formularz (multipart/form-data)
        WWWForm form = new WWWForm();
        // "file" to nazwa pola, któr¹ musi obs³u¿yæ serwer
        form.AddBinaryData("file", fileData, fileName, "text/csv");

        // Tworzymy ¿¹danie
        using (UnityWebRequest www = UnityWebRequest.Post(serverUploadUrl, form))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[FileUploader] B³¹d wysy³ania: {www.error} | {www.downloadHandler.text}");
            }
            else
            {
                Debug.Log($"[FileUploader] SUKCES! Plik wys³any na serwer: {fileName}");

                // Opcjonalnie: Usuñ plik z gogli po udanym wys³aniu, ¿eby nie zapychaæ pamiêci
                // File.Delete(filePath); 
            }
        }
    }
}