using Telemetry.Session;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeveloperTools
{

    public class DevMenuActions : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DeveloperModeConfig config;

        [Header("Scene Names")]
        [SerializeField] private string mainMenuScene = "MainMenu";
        [SerializeField] private string room1Scene = "Room_1";
        [SerializeField] private string room2Scene = "Room_2";
        [SerializeField] private string room3Scene = "Room_3";
        [SerializeField] private string room4Scene = "Room_4";

        private void Awake()
        {
            if (config == null)
                config = GetComponentInParent<DeveloperModeConfig>();
        }

        public void StopSession()
        {
            if (config != null && !config.AllowStopSession)
                return;

            if (TelemetrySessionManager.Instance != null &&
                TelemetrySessionManager.Instance.IsRecording)
            {
                TelemetrySessionManager.Instance.StopSession();
            }
        }

        public void BackToMainMenu()
        {
            LoadScene(mainMenuScene);
        }

        private void LoadRoom1() => LoadScene(room1Scene);
        private void LoadRoom2() => LoadScene(room2Scene);
        private void LoadRoom3() => LoadScene(room3Scene);
        private void LoadRoom4() => LoadScene(room4Scene);

        private void LoadScene(string sceneName)
        {
            if (config != null && !config.AllowSceneTeleport)
                return;

            if (string.IsNullOrWhiteSpace(sceneName))
                return;

            SceneManager.LoadScene(sceneName);
        }
    }
}