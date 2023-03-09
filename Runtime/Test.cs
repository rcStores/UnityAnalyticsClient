#if UNITY_ANDROID
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Advant
{
	internal class PluginWrapper
	{
		public string GetString()
		{
			using var scanner = new AndroidJavaClass("com.example.plugintest.TestPlugin");
			return scanner.CallStatic<string>("foo");
		}
	}
}
#endif