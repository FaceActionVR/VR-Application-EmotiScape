using System;

namespace Telemetry.Writers
{
    [Serializable]
    public class BufferedWriterSettings
    {
        public int StreamBufferSizeBytes = 65536;
        public int FlushLineThreshold = 100;
    }
}