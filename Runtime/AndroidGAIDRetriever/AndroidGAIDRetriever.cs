
using System;
using UnityEngine;

namespace RosUtils
{
public static class AndroidGAIDRetriever
{
	public static void GetAsync(System.Action<string> cb)
	{
		var receiver = new GAIDReceiver(cb);

		using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
		using var activity    = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
		using var context     = activity.Call<AndroidJavaObject>("getApplicationContext");
		using var retriever   = new AndroidJavaClass("com.rosdvp.androidutils.GAIDRetriever");
		retriever.CallStatic("getGAID", context, receiver);
	}

	private class GAIDReceiver : AndroidJavaProxy
	{
		private System.Action<string> _cb;
		public GAIDReceiver(System.Action<string> cb) : base("com.rosdvp.androidutils.GAIDRetriever$IGAIDReceiver")
		{
			_cb = cb;
		}

		/// <summary>
		/// Overriding because sometimes OnGAIDReceived(AndroidJavaObject) was called.
		/// Reason why - unknown.
		/// </summary>
		public override AndroidJavaObject Invoke(string methodName, object[] args)
		{
			if (methodName == "OnGAIDReceived" && args.Length > 0 && args[0] is string gaid)
			{
				OnGAIDReceived(gaid);
				return null;
			}
			try
			{
				return base.Invoke(methodName, args);
			}
			catch (Exception e)
			{
				Debug.Log($"Unknown proxy method: {e.Message}\n{e.StackTrace}");
				return null;
			}
		}

		public void OnGAIDReceived(string gaid)
		{
			_cb?.Invoke(gaid);
		}
	}
}
}
