#if UNITY_ANDROID
using UnityEngine;

namespace AndroidUtils
{
	internal static class ApiUtil
	{
		public static int GetApiVersion()
		{
			using var version = new AndroidJavaClass("android.os.Build$VERSION");
			return version.GetStatic<int>("SDK_INT");
		}
		
		/// <summary>
		/// Since Android API 28 (or something around) Application.persistentDataPath returns the incorrect path
		/// that create AccessDenied exception; use this instead.
		/// </summary>
		public static string GetPersistentDataPath()
		{
			using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
			using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
			using var context = activity.Call<AndroidJavaObject>("getApplicationContext");
			using var filesDir = context.Call<AndroidJavaObject>("getFilesDir");
			var filesPath = filesDir.Call<string>("getCanonicalPath");
			return filesPath;
		}
	}
}
#endif