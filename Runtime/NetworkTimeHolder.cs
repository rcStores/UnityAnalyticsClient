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
	
	public static async UniTask<DateTime> InitAsync(Backend backend)
	{
		_backend = backend;
		await SynchronizeTimeAsync();
		return UtcNow;
	}
	
	public static async UniTask SynchronizeTimeAsync()
	{
		if (DateTime.UtcNow.Subtract(_systemInitialTime) > TimeSpan.FromSeconds(60) && DateTime.UtcNow > _systemInitialTime)
		{
			try
			{
				_networkInitialTime = _backend is null ? 
					DateTime.UtcNow :
					await _backend.GetNetworkTime();
				_systemInitialTime = DateTime.UtcNow;
				Debug.LogWarning($"[ADVANT] System time = {_systemInitialTime}, network time = {_networkInitialTime}");
			}
			catch (Exception e)
			{
				Debug.LogError("Unexpected error while getting network time: " + e.Message);
				Debug.LogError(e.StackTrace);
				_networkInitialTime = DateTime.UtcNow;
			}
			
			
			Debug.LogWarning($"[ADVANT] _systemInitialTime.Subtract(_networkInitialTime) = {_systemInitialTime.Subtract(_networkInitialTime)}");
			Debug.LogWarning($"[ADVANT] _networkInitialTime.Subtract(_systemInitialTime) = {_systemInitialTime.Subtract(_networkInitialTime)}");
			if (_systemInitialTime.Subtract(_networkInitialTime) > TimeSpan.FromSeconds(10))
			{
				Debug.LogWarning($"[ADVANT] System time was changed by the user");
				_isSystemTimeDifferent = true;
			}
			Debug.LogWarning($"[ADVANT] _isSystemTimeDifferent = {_isSystemTimeDifferent}");
		}
	}
}