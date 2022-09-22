using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

using Advant.Http;

internal static class RealDateTime
{
	public static DateTime UtcNow
	{ 
		get => _isSystemTimeDifferent && DateTime.UtcNow > _systemInitialTime ? 
			_networkInitialTime.AddSeconds((DateTime.UtcNow - _systemInitialTime).TotalSeconds) :
			DateTime.UtcNow; 
	}
		
	private static Backend _backend;
	
	private static DateTime _systemInitialTime;
	private static DateTime _networkInitialTime;
	
	private static bool _isSystemTimeDifferent;
	
	public static async UniTask InitAsync(Backend backend)
	{
		_backend = backend;
		await SynchronizeTimeAsync();
	}
	
	public static async UniTask SynchronizeTimeAsync()
	{
		try
		{
			_networkInitialTime = _backend is null ? 
				DateTime.UtcNow :
				await _backend.GetNetworkTime();
		}
		catch (Exception e)
		{
			Debug.LogError("Unexpected error while getting network time: " + e.Message);
			Debug.LogError(e.StackTrace);
			_networkInitialTime = DateTime.UtcNow;
		}
			
		_systemInitialTime = DateTime.UtcNow;
		
		if (_networkInitialTime - _systemInitialTime > TimeSpan.FromSeconds(10))
			_isSystemTimeDifferent = true;
	}
}