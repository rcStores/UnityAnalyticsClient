using System;
using UnityEngine;
using UniTask;

namespace Advant.AndroidUtils
{
public static class AndroidWebRequestWrapper
{
	public static void GetAsync(string endpoint, System.Action<HttpResponse> cb)
	{
		var receiver = new WebRequestResultReceiver(cb);

		using var retriever   = new AndroidJavaClass("com.advant.androidutils.AndroidWebRequestExecutor");
		retriever.CallStatic("executeWebRequest", receiver, "GET", endpoint, null);
		
		return result;
	}
	
	public static void PostAsync(string endpoint, string data, System.Action<HttpResponse> cb)
	{
		var receiver = new WebRequestResultReceiver(cb);

		using var retriever   = new AndroidJavaClass("com.advant.androidutils.AndroidWebRequestExecutor");
		retriever.CallStatic("executeWebRequest", receiver, "POST", endpoint, data);

		return result;
	}
	
	public static void PutAsync(string endpoint, string data, System.Action<HttpResponse> cb)
	{
		var receiver = new WebRequestResultReceiver(cb);

		using var retriever   = new AndroidJavaClass("com.advant.androidutils.AndroidWebRequestExecutor");
		var resultingJavaObj = retriever.CallStatic("executeWebRequest", receiver, "PUT", endpoint, data);
		
		return result;
	}

	private class WebRequestResultReceiver : AndroidJavaProxy
	{
		private System.Action<HttpResponse> _cb;
		public WebRequestResultReceiver(System.Action<HttpResponse> cb) : base("com.advant.androidutils.AndroidWebRequestExecutor$IWebRequestResultReceiver")
		{
			_cb = cb;
		}

		/// <summary>
		/// Overriding because sometimes OnResponseReceived(AndroidJavaObject) was called.
		/// Reason why - unknown.
		/// </summary>
		public override AndroidJavaObject Invoke(string methodName, object[] args)
		{
			if (methodName == "OnResponseReceived" && args.Length > 0 && args[0] is HttpResponse output)
			{
				OnResponseReceived(output);
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

		public void OnResponseReceived(HttpResponse output)
		{
			_cb?.Invoke(output);
		}

		//Dummy method, because for some reason this signature is called by proxy.
		//That throws exception if there is no such method.
		public void OnResponseReceived(AndroidJavaObject obj)
		{
			Debug.Log("OnResponseReceived(AndroidJavaObject) called");
		}
	}
}
}