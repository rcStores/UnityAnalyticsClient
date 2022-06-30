#if UNITY_EDITOR
#define ENABLE_LOGS 1
#endif

using System.Diagnostics;

namespace Advant.Logging
{
    internal static class Log
    {
        [Conditional("ENABLE_LOGS")]
        public static void Info(string logMsg)
        {
            UnityEngine.Debug.Log(logMsg);
        }
    }
}
