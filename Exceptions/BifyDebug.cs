using System;
using System.IO;
using System.Runtime.CompilerServices;
using Spectre.Console;

namespace BifyLang.Exceptions
{
    internal class BifyDebug
    {
        private static readonly object _lock = new();
        private static string _logFilePath = "bify.log";

        public static void SetLogFile(string logFilePath, bool reset = true)
        {
            lock (_lock)
            {
                _logFilePath = string.IsNullOrWhiteSpace(logFilePath) ? "bify.log" : logFilePath;
                if (reset)
                {
                    File.WriteAllText(_logFilePath, string.Empty);
                }
            }
        }

        public static void Log(
            string message,
            [CallerMemberName] string callerName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0)
        {
            LogInternal("INFO", message, callerName, callerFilePath, callerLineNumber);
        }

        public static void LogWarning(
            string message,
            [CallerMemberName] string callerName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0)
        {
            LogInternal("WARNING", message, callerName, callerFilePath, callerLineNumber);
        }

        public static void LogError(
            string message,
            [CallerMemberName] string callerName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0)
        {
            LogInternal("ERROR", message, callerName, callerFilePath, callerLineNumber);
        }

        public static void LogException(
            Exception exception,
            [CallerMemberName] string callerName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0)
        {
            LogInternal("EXCEPTION", exception.ToString(), callerName, callerFilePath, callerLineNumber);
        }

        public static void Assert(
            bool condition,
            string message = "Assertion failed.",
            [CallerMemberName] string callerName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0)
        {
            if (!condition)
            {
                LogInternal("ASSERT", message, callerName, callerFilePath, callerLineNumber);
                throw new InvalidOperationException(message);
            }
        }

        private static void LogInternal(
            string level,
            string? message,
            string? callerName,
            string callerFilePath,
            int callerLineNumber)
        {
            string fileName = Path.GetFileName(callerFilePath);
            string time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string logMessage = $"[{callerName ?? string.Empty}] [{level}] [{time}] {message ?? string.Empty} - {fileName}:{callerLineNumber}";

            lock (_lock)
            {
                File.AppendAllText(_logFilePath, logMessage + Environment.NewLine);
            }
        }
    }
}
