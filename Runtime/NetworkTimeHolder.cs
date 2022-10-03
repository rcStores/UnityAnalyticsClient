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
	
	private static DateTime _systemInitialTime	= DateTime.UtcNow;
	private static DateTime _networkInitialTime	= DateTime.UtcNow;
	
	private static UniTask _networkTask;
	
	private static bool _isSystemTimeDifferent;
	
	public static async UniTask<DateTime> InitAsync(Backend backend)
	{
		_backend = backend;
		await SynchronizeTimeAsync(isCalledOnInit: true);
		return UtcNow;
	}
	
	public static async UniTask<DateTime> SynchronizeTimeAsync(bool isCalledOnInit = false)
	{
		if (DateTime.UtcNow.Subtract(_systemInitialTime) > TimeSpan.FromSeconds(60) && DateTime.UtcNow > _systemInitialTime || isCalledOnInit)
		{	
			_networkInitialTime = _systemInitialTime = DateTime.UtcNow;
			_networkTask = _backend.GetNetworkTime().Preserve();
			while (await _networkTask is _networkInitialTime)
            {
#if UNITY_EDITOR
				if (!Application.isPlaying)
					return;
#endif				
                if (_networkInitialTime == default)
                {
                    _networkInitialTime = _systemInitialTime = DateTime.UtcNow;
					await UniTask.Delay(
						TimeSpan.FromMinutes(2), 
						false, 
						PlayerLoopTiming.PostLateUpdate);
                    Log.Info("Retry time synchronization");
					Debug.LogWarning("[ADVANAL] Time synchronization failed. Retry...");
                }
                else
                {
                    //_systemInitialTime = DateTime.UtcNow;
                    if (_systemInitialTime.Subtract(_networkInitialTime) > TimeSpan.FromSeconds(10))
					{
						Debug.LogWarning($"[ADVANT] System time was changed by the user");
						_isSystemTimeDifferent = true;
					}
					Debug.LogWarning($"[ADVANT] _isSystemTimeDifferent = {_isSystemTimeDifferent}");
					break;
                }
            }
			return _networkInitialTime;
		}
	}
	
	public static async UniTask<DateTime> ExposeAsync(DateTime timestamp) 
	{
		await _networkTask; // is similar to awaiting on SynchronizeTime itself in a synchronous code 
		return _isSystemTimeDifferent && timestamp > _systemInitialTime ? 
			_networkInitialTime.AddSeconds((timestamp - _systemInitialTime).TotalSeconds) :
			timestamp;
	}
	

	// public static async UniTask SynchronizeTimeAsync(bool isCalledOnInit = false)
	// {
		// if (DateTime.UtcNow.Subtract(_systemInitialTime) > TimeSpan.FromSeconds(60) && DateTime.UtcNow > _systemInitialTime || isCalledOnInit)
		// {
			// if (_backend is not null)
			// {
				// _networkTask = _backend.GetNetworkTime().Preserve();
			// try
			// {
				// _networkInitialTime = _backend is null ? 
					// DateTime.UtcNow :
					// await _networkTask;
					// // if error occurs, _isNetworkReached = false;
			// }
			// catch (Exception e)
			// {
				// Debug.LogError("Unexpected error while getting network time: " + e.Message);
				// Debug.LogError(e.StackTrace);
				// _networkInitialTime = DateTime.UtcNow;
			// }
			// _systemInitialTime = DateTime.UtcNow;
			// Debug.LogWarning($"[ADVANT] System time = {_systemInitialTime}, network time = {_networkInitialTime}");
			
			// Debug.LogWarning($"[ADVANT] _systemInitialTime.Subtract(_networkInitialTime) = {_systemInitialTime.Subtract(_networkInitialTime)}");
			// Debug.LogWarning($"[ADVANT] _networkInitialTime.Subtract(_systemInitialTime) = {_systemInitialTime.Subtract(_networkInitialTime)}");
			// if (_systemInitialTime.Subtract(_networkInitialTime) > TimeSpan.FromSeconds(10))
			// {
				// Debug.LogWarning($"[ADVANT] System time was changed by the user");
				// _isSystemTimeDifferent = true;
			// }
			// Debug.LogWarning($"[ADVANT] _isSystemTimeDifferent = {_isSystemTimeDifferent}");
		// }
	// }	
}