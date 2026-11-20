using System.IO;
using UnityEngine;

namespace Telemetry.Session
{
    public class SessionPathProvider : MonoBehaviour
    {
        [Header("Storage")]
        [SerializeField] private string rootFolderName = "sessions";

        public string CurrentSessionFolder { get; private set; }

        public string CreateSessionFolder(string sessionId)
        {
            string safeSessionId = SanitizeFileName(sessionId);

            CurrentSessionFolder = Path.Combine(
                Application.persistentDataPath,
                rootFolderName,
                safeSessionId
            );

            Directory.CreateDirectory(CurrentSessionFolder);

            return CurrentSessionFolder;
        }

        public string GetFilePath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(CurrentSessionFolder))
            {
                Debug.LogError("[SessionPathProvider] Session folder is not initialized.");
                return null;
            }

            return Path.Combine(CurrentSessionFolder, fileName);
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');

            return value;
        }
    }
}