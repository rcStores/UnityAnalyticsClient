using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using System.Globalization;

using Advant.Http;
using Advant.Data;
using Advant.Data.Models;

internal class NetworkTimeHolder
{
	private DateTime _systemInitialTime;
	private DateTime _networkInitialTime;
	
	private bool _isFirstInit;
	
	private readonly Backend _backend;
	
	public NetworkTimeHolder(Backend backend)
	{
		_backend = backend;
		_systemInitialTime = DateTime.UtcNow;
		_isFirstInit = true;
	}
	
	//public DateTime CurrentTime { get => _networkInitialTime.AddSeconds((DateTime.UtcNow - _systemInitialTime).TotalSeconds); }
	
	public bool IsServerReached { get => _networkInitialTime != default; }
	
	public DateTime GetVerifiedTime(DateTime timestamp) => _networkInitialTime.AddSeconds((timestamp - _systemInitialTime).TotalSeconds);
	
	// {	if (IsServerReached)
		// {
			// TimeSpan delta;	
			// if (timestamp > _systemInitialTime)
				// delta = timestamp - _systemInitialTime;
			// else
				// delta = _systemInitialTime - timestamp;
			// return _networkInitialTime.AddSeconds(delta.TotalSeconds);
		// }
		// else return timestamp;
	// }
		
	
	// нескольо запросов подряд?
	public async UniTask<DateTime> GetInitialTimeAsync() 
	{
		var timeSincePrevInit = timestamp - _systemInitialTime;
		if (_networkInitialTime != default && timeSincePrevInit.TotalSeconds > 0 && timeSincePrevInit.TotalSeconds <= 1) return _networkInitialTime;
		
		_networkInitialTime = default;
		_systemInitialTime = _isFirstInit? _systemInitialTime : DateTime.UtcNow;
		_isFirstInit = false;
		while (true)
        {
#if UNITY_EDITOR
			if (!Application.isPlaying)
				return default;
#endif		
			var currentNetworkTime = await _backend.GetNetworkTime();
			
            if (currentNetworkTime == default)
            {
				await UniTask.Delay(
					TimeSpan.FromMinutes(2), 
					false, 
					PlayerLoopTiming.PostLateUpdate);
				Debug.LogWarning("[ADVANAL] Time synchronization failed. Retry...");
            }
            else
            {
				Debug.LogWarning($"[ADVANAL] _networkInitialTime.AddTicks((currentNetworkTime - (DateTime.UtcNow - _systemInitialTime)).Ticks) = {_networkInitialTime}.AddTicks(({currentNetworkTime} - ({DateTime.UtcNow} - {_systemInitialTime})).Ticks");
				_networkInitialTime = _networkInitialTime.AddTicks((currentNetworkTime - (DateTime.UtcNow - _systemInitialTime)).Ticks);
				Debug.LogWarning("[ADVANAL] network initial time: " + _networkInitialTime);
				break;
            }
        }
		return _networkInitialTime;
	}
	
	// public async UniTask<DateTime?> GetTimeAsync()
	// {
		// DateTime networkTime = await _backend.GetNetworkTime();
		// if (networkTime != default)
			// _networkInitialTime.AddSeconds((networkTime - DateTime.UtcNow + _systemInitialTime).TotalSeconds);
		// return Now;
	// }
	
	public void ValidateTimestamps(GamePropertiesPool pool)
	{
		if (!IsServerReached) return;
		
		for (int i = 0; i < pool.GetCurrentBusyCount(); ++i)
		{
			ValidateTimestamps(ref pool.Elements[i]);
		}
	}
	
	public void ValidateTimestamps(GameEventsPool pool)
	{
		if (!IsServerReached) return;
		
		for (int i = 0; i < pool.GetCurrentBusyCount(); ++i)
		{
			ValidateTimestamps(ref pool.Elements[i]);
		}
	}
	
	public void ValidateTimestamps(GameSessionsPool pool)
	{
		if (pool.HasCurrentSession() && IsServerReached)
			ValidateTimestamps(ref pool.CurrentSession());
	}
	
	public void ValidateTimestamps(ref GameProperty p)
	{
		// ValidateTimestamps(ref p.GetValue());
	}
	
	public void ValidateTimestamps(ref Session s)
	{
		if (s.HasValidTimestamps) return;
		
		try 
		{
			s.LastActivity = GetVerifiedTime(s.LastActivity);
			s.HasValidTimestamps = true;
		}
		catch (Exception e)
		{
			Debug.LogWarning($"[ADVANT] Error while validating Session timestamp. Session number: {s.SessionCount}, error message: {e.Message}");
		}
	}
	
	public void ValidateTimestamps(ref GameEvent e)
	{
		if (e.HasValidTimestamps) return;
		
		try 
		{
			e.Time = GetVerifiedTime(e.Time);
			for (int i = 0; i < e.ParamsCount; ++i)
			{
				ValidateTimestamps(ref e.Params[i]);
			}
			e.HasValidTimestamps = true;
		}
		catch (Exception e)
		{
			Debug.LogWarning($"[ADVANT] Error while validating GameEvent timestamp. Event name: {e.Name}, error message: {e.Message}");
		}
	}
	
	public void ValidateTimestamps(ref Value v)
	{
		if (v.Type == Value.EValueType.DateTime)
		{
			// it's so fucking nasty, i hate it
			var timestamp = DateTime.ParseExact(v.Data,"yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture);
			v.Set(v.Name, GetVerifiedTime(timestamp));
		}
	}
	
	private static DateTime MinDate(DateTime val1, DateTime val2)
    {
        return DateTime.FromBinary(
            Math.Min(val1.ToBinary(), val2.ToBinary()));
    }

	private static DateTime MaxDate(DateTime val1, DateTime val2)
    {
		return DateTime.FromBinary(
			Math.Max(val1.ToBinary(), val2.ToBinary()));
    }
}

// not static
// internal static class RealDateTime
// {
	// // nullable?
	// public static DateTime UtcNow // remove or nullable 
	// { 
		// get => _isSystemTimeDifferent && DateTime.UtcNow > _systemInitialTime ? 
			// _networkInitialTime.AddSeconds((DateTime.UtcNow - _systemInitialTime).TotalSeconds) :
			// DateTime.UtcNow; 
	// }
		
	// private static Backend _backend;
	
	// private static DateTime _systemInitialTime	= DateTime.UtcNow;
	// private static DateTime _networkInitialTime	= DateTime.UtcNow;
	
	// private static UniTask _networkTask;
	
	// private static bool _isSystemTimeDifferent; // gtfo
	
	// public static async UniTask<DateTime> InitAsync(Backend backend)
	// {
		// _backend = backend;
		// await SynchronizeTimeAsync(isCalledOnInit: true);
		// return UtcNow;
	// }
	
	// // GetTimeAsync:
	// // 1 - fire and forget
	// // 2 - await 
	// // _isServerReached
	// // GetTimeAsync
	// // ForceGettingTime: cancel prev tasl, flag = false await GetTimeAsync
	// public static async UniTask<DateTime> SynchronizeTimeAsync(bool isCalledOnInit = false)
	// {
		// // minute is too much
		// // 60 const
		// if (DateTime.UtcNow.Subtract(_systemInitialTime) > TimeSpan.FromSeconds(60) && DateTime.UtcNow > _systemInitialTime || isCalledOnInit) // rewrite
		// {	
			// _networkInitialTime = _systemInitialTime = DateTime.UtcNow;
			// _networkTask = _backend.GetNetworkTime().Preserve();
			// while (await _networkTask is _networkInitialTime)
            // {
// #if UNITY_EDITOR
				// if (!Application.isPlaying)
					// return;
// #endif				
                // if (_networkInitialTime == default)
                // {
                    // _networkInitialTime = _systemInitialTime = DateTime.UtcNow;
					// await UniTask.Delay(
						// TimeSpan.FromMinutes(2), 
						// false, 
						// PlayerLoopTiming.PostLateUpdate);
					// Debug.LogWarning("[ADVANAL] Time synchronization failed. Retry...");
                // }
                // else
                // {
                    // //_systemInitialTime = DateTime.UtcNow;
                    // if (_systemInitialTime.Subtract(_networkInitialTime) > TimeSpan.FromSeconds(10))
					// {
						// Debug.LogWarning($"[ADVANT] System time was changed by the user");
						// _isSystemTimeDifferent = true;
					// }
					// Debug.LogWarning($"[ADVANT] _isSystemTimeDifferent = {_isSystemTimeDifferent}");
					// break;
                // }
            // }
		// }
		// return _networkInitialTime;
	// }
	
	// public static async UniTask<DateTime> ExposeAsync(DateTime timestamp) 
	// {
		// await _networkTask; // is similar to awaiting on SynchronizeTime itself in a synchronous code 
		// return _isSystemTimeDifferent && timestamp > _systemInitialTime ? 
			// _networkInitialTime.AddSeconds((timestamp - _systemInitialTime).TotalSeconds) :
			// timestamp;
	// }
	

	// // public static async UniTask SynchronizeTimeAsync(bool isCalledOnInit = false)
	// // {
		// // if (DateTime.UtcNow.Subtract(_systemInitialTime) > TimeSpan.FromSeconds(60) && DateTime.UtcNow > _systemInitialTime || isCalledOnInit)
		// // {
			// // if (_backend is not null)
			// // {
				// // _networkTask = _backend.GetNetworkTime().Preserve();
			// // try
			// // {
				// // _networkInitialTime = _backend is null ? 
					// // DateTime.UtcNow :
					// // await _networkTask;
					// // // if error occurs, _isNetworkReached = false;
			// // }
			// // catch (Exception e)
			// // {
				// // Debug.LogError("Unexpected error while getting network time: " + e.Message);
				// // Debug.LogError(e.StackTrace);
				// // _networkInitialTime = DateTime.UtcNow;
			// // }
			// // _systemInitialTime = DateTime.UtcNow;
			// // Debug.LogWarning($"[ADVANT] System time = {_systemInitialTime}, network time = {_networkInitialTime}");
			
			// // Debug.LogWarning($"[ADVANT] _systemInitialTime.Subtract(_networkInitialTime) = {_systemInitialTime.Subtract(_networkInitialTime)}");
			// // Debug.LogWarning($"[ADVANT] _networkInitialTime.Subtract(_systemInitialTime) = {_systemInitialTime.Subtract(_networkInitialTime)}");
			// // if (_systemInitialTime.Subtract(_networkInitialTime) > TimeSpan.FromSeconds(10))
			// // {
				// // Debug.LogWarning($"[ADVANT] System time was changed by the user");
				// // _isSystemTimeDifferent = true;
			// // }
			// // Debug.LogWarning($"[ADVANT] _isSystemTimeDifferent = {_isSystemTimeDifferent}");
		// // }
	// // }	
// }