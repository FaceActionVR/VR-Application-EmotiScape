using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Telemetry.Writers
{
    public class BufferedCsvWriter
    {
        private readonly BufferedWriterSettings settings;
        private readonly StringBuilder buffer = new StringBuilder();

        private StreamWriter streamWriter;
        private int bufferedLineCount;

        public bool IsOpen => streamWriter != null;
        public string CurrentFilePath { get; private set; }

        public BufferedCsvWriter(BufferedWriterSettings settings)
        {
            this.settings = settings ?? new BufferedWriterSettings();
        }

        public void Open(string filePath, string header = null)
        {
            if (IsOpen)
            {
                Debug.LogWarning("[BufferedCsvWriter] Writer already open.");
                return;
            }

            try
            {
                CurrentFilePath = filePath;

                streamWriter = new StreamWriter(
                    filePath,
                    false,
                    Encoding.UTF8,
                    settings.StreamBufferSizeBytes
                );

                if (!string.IsNullOrWhiteSpace(header))
                {
                    streamWriter.WriteLine(header);
                }

                Debug.Log($"[BufferedCsvWriter] Opened: {filePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[BufferedCsvWriter] Failed to open file:\n{e}");
            }
        }

        public void WriteLine(string line)
        {
            if (!IsOpen)
            {
                Debug.LogWarning("[BufferedCsvWriter] Write attempted while closed.");

                return;
            }

            buffer.AppendLine(line);
            bufferedLineCount++;

            if (bufferedLineCount >= settings.FlushLineThreshold)
            {
                Flush();
            }
        }

        public void Flush()
        {
            if (!IsOpen || buffer.Length == 0)
                return;

            try
            {
                streamWriter.Write(buffer.ToString());
                buffer.Clear();
                bufferedLineCount = 0;
            }
            catch (Exception e)
            {
                Debug.LogError($"[BufferedCsvWriter] Flush failed:\n{e}");
            }
        }

        public void Close()
        {
            if (!IsOpen)
                return;

            try
            {
                Flush();
                streamWriter.Close();
                streamWriter.Dispose();
                streamWriter = null;

                Debug.Log($"[BufferedCsvWriter] Closed: {CurrentFilePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[BufferedCsvWriter] Close failed:\n{e}");
            }
        }
    }
}
