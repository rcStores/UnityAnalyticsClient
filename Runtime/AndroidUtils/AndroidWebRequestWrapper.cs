#if UNITY_ANDROID
using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Advant.Http;

namespace AndroidUtils
{
public static class AndroidWebRequestWrapper
{
	public static void GetAsync(
		string endpoint, 
		System.Action<string, int, string, string> cb,
		System.Action<string> errorCallback)
	{
		var receiver = new WebRequestResultReceiver(cb, errorCallback);

		using var retriever   = new AndroidJavaClass("com.advant.androidutils.AndroidWebRequestExecutor");
		retriever.CallStatic("executeWebRequest", receiver, "GET", endpoint, null);
	}
	
	public static void PostAsync(
		string endpoint, 
		string data, 
		System.Action<string, int, string, string> cb,
		System.Action<string> errorCallback)
	{
		var receiver = new WebRequestResultReceiver(cb, errorCallback);

		using var retriever   = new AndroidJavaClass("com.advant.androidutils.AndroidWebRequestExecutor");
		retriever.CallStatic("executeWebRequest", receiver, "POST", endpoint, data);
	}
	
	public static void PutAsync(
		string endpoint, 
		string data, 
		System.Action<string, int, string, string> cb,
		System.Action<string> errorCallback)
	{
		var receiver = new WebRequestResultReceiver(cb, errorCallback);

		using var retriever   = new AndroidJavaClass("com.advant.androidutils.AndroidWebRequestExecutor");
		retriever.CallStatic("executeWebRequest", receiver, "PUT", endpoint, data);
	}

	private class WebRequestResultReceiver : AndroidJavaProxy
	{
		private System.Action<string, int, string, string> _cb;
		private System.Action<string> _errorCallback;
		
		public WebRequestResultReceiver(
			System.Action<string, int, string, string> cb, System.Action<string> errorCallback) : base("com.advant.androidutils.AndroidWebRequestExecutor$IWebRequestResultReceiver")
		{
			_cb = cb;
			_errorCallback = errorCallback;
		}

		/// <summary>
		/// Overriding because sometimes OnResponseReceived(AndroidJavaObject) was called.
		/// Reason why - unknown.
		/// </summary>
		public override AndroidJavaObject Invoke(string methodName, object[] args)
		{
			if (methodName == "OnResponseReceived" && 
				args.Length > 0 && 
				args[0] is string data && 
				args[1] is int code &&
				args[2] is string message &&
				args[3] is string error)
			{
				OnResponseReceived(data, code, message, error);
				return null;
			}
			else if (methodName == "OnError" && 
				args.Length > 0 && 
				args[0] is string error)
			{
				OnError(error);
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

		public void OnResponseReceived(string data, int code, string message, string error)
		{
			_cb?.Invoke(data, code, message, error);
		}
		
		public void OnError(string error)
		{
			_errorCallback?.Invoke(error);
		}

		//Dummy method, because for some reason this signature is called by proxy.
		//That throws exception if there is no such method.
		public void OnResponseReceived(AndroidJavaObject obj)
		{
			Debug.Log("OnResponseReceived(AndroidJavaObject) called");
		}
		
		//Dummy method, because for some reason this signature is called by proxy.
		//That throws exception if there is no such method.
		public void OnError(AndroidJavaObject obj)
		{
			Debug.Log("OnError(AndroidJavaObject) called");
		}
	}
}
}
#endif