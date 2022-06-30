#define ENABLE_LOGS UNITY_EDITOR

using System.Diagnostics;

namespace Logging
{
    internal static class Logger
    {
        [Conditional("ENABLE_LOGS")]
        public static void Log(string logMsg)
        {
            UnityEngine.Debug.Log(logMsg);
        }
    }
}
