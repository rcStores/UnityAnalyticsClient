#if UNITY_ANDROID
using System;
using UnityEngine;

namespace RosUtils
{
internal static class AndroidGAIDRetriever
{
	public static void GetAsync(System.Action<string> cb)
	{
		var receiver = new GAIDReceiver(cb);

		using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
		using var activity    = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
		using var context     = activity.Call<AndroidJavaObject>("getApplicationContext");
		using var retriever   = new AndroidJavaClass("com.rosdvp.androidutils.AdvantGAIDRetriever");
		retriever.CallStatic("getGAID", context, receiver);
	}

	private class GAIDReceiver : AndroidJavaProxy
	{
		private System.Action<string> _cb;
		public GAIDReceiver(System.Action<string> cb) : base("com.rosdvp.androidutils.AdvantGAIDRetriever$IGAIDReceiver")
		{
			_cb = cb;
		}
		
		public void OnGAIDReceived(string gaid)
		{
			_cb?.Invoke(gaid);
		}
		
		public void OnGAIDReceived(AndroidJavaObject obj)
		{
			_cb.Invoke(null);
		}
	}
}
}
#endif
