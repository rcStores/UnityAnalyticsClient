using System.Diagnostics;

namespace Advant.Logging
{
    internal static class Log
    {
        [Conditional("UNITY_EDITOR")]
        public static void Info(string logMsg)
        {
            UnityEngine.Debug.Log(logMsg);
        }
    }
}
