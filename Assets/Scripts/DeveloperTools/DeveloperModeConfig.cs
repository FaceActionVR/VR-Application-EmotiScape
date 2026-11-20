using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DeveloperTools
{
    public class DeveloperModeConfig : MonoBehaviour
    {
        [Header("Developer Mode")]
        [SerializeField] private bool debugMode = true;

        [Header("Permissions")]
        [SerializeField] private bool allowStopSession = true;
        [SerializeField] private bool allowSceneTeleport = true;

        public bool DebugMode => debugMode;
        public bool AllowStopSession => allowStopSession;
        public bool AllowSceneTeleport => allowSceneTeleport;
    }
}