using System;

namespace Telemetry.Models
{
    [Serializable]
    public struct FaceTrackingSample
    {
        public double Timestamp;

        public float[] Eyes;

        public float[] Lips;

        public FaceTrackingSample(
            double timestamp,
            float[] eyes,
            float[] lips)
        {
            Timestamp = timestamp;
            Eyes = eyes;
            Lips = lips;
        }
    }
}