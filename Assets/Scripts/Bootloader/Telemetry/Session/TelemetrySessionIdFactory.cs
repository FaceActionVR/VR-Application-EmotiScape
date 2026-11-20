using System;
using System.IO;

namespace Telemetry.Session
{
    public static class TelemetrySessionIdFactory
    {
        public static string GenerateSessionId(string user)
        {
            string safeUser = string.IsNullOrWhiteSpace(user)
                ? "UNKNOWN_USER"
                : user.Trim().Replace(" ", "_");

            foreach (char invalidChar in Path.GetInvalidFileNameChars())
                safeUser = safeUser.Replace(invalidChar, '_');

            return $"{safeUser}_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
        }
    }
}