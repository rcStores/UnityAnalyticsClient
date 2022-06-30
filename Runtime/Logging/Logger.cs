#if UNITY_EDITOR
#define ENABLE_LOGS
#endif

using System.Diagnostics;

namespace Advant.Logging
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
