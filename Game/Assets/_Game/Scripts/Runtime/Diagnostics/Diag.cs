using System;
using System.Collections.Generic;
using UnityEngine;

namespace ANL.Runtime.Diagnostics
{
    public static class Diag
    {
        private const int MaxEntries = 200;
        private static readonly object Gate = new object();
        private static readonly Queue<string> ErrorEntries = new Queue<string>(MaxEntries);

        public static IReadOnlyList<string> RecentErrors
        {
            get
            {
                lock (Gate)
                {
                    return new List<string>(ErrorEntries);
                }
            }
        }

        public static void Info(string message)
        {
            Log("INFO", message);
        }

        public static void Warn(string message)
        {
            Log("WARN", message);
        }

        public static void Error(string message)
        {
            var line = Log("ERROR", message);
            lock (Gate)
            {
                ErrorEntries.Enqueue(line);
                while (ErrorEntries.Count > MaxEntries) ErrorEntries.Dequeue();
            }
        }

        public static void Clear()
        {
            lock (Gate)
            {
                ErrorEntries.Clear();
            }
        }

        private static string Log(string level, string message)
        {
            var line = DateTime.UtcNow.ToString("o") + " [" + level + "] " + (message ?? "");
            if (level == "ERROR") Debug.LogError(line);
            else if (level == "WARN") Debug.LogWarning(line);
            else Debug.Log(line);
            return line;
        }
    }
}
