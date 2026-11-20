using System;
using UnityEngine;

namespace Telemetry.Models
{
    [Serializable]
    public struct PhysicsTrackingSample
    {
        public double Timestamp;
        public int RoomId;

        public Vector3 HeadPosition;
        public Quaternion HeadRotation;

        public PhysicsTrackingSample(
            double timestamp,
            int roomId,
            Vector3 headPosition,
            Quaternion headRotation)
        {
            Timestamp = timestamp;
            RoomId = roomId;
            HeadPosition = headPosition;
            HeadRotation = headRotation;
        }
    }
}