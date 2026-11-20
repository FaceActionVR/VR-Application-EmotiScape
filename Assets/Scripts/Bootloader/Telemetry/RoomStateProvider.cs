using UnityEngine;

namespace Telemetry.SceneState
{
    public class RoomStateProvider : MonoBehaviour
    {
        [Header("Room State")]
        [SerializeField] private int currentRoomId = 1;

        [SerializeField] private string currentRoomName = "Room_1";

        public int CurrentRoomId => currentRoomId;

        public string CurrentRoomName => currentRoomName;

        public void SetRoom(int roomId, string roomName = null)
        {
            currentRoomId = roomId;

            if (!string.IsNullOrWhiteSpace(roomName))
                currentRoomName = roomName;

            Debug.Log($"[RoomStateProvider] Current room: {currentRoomId} ({currentRoomName})");
        }
    }
}