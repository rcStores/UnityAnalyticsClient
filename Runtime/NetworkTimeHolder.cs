using System;
using System.Threading;
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
	
	private bool _isLoopRunning = false;
	
	private bool _isFirstInit;
	
	private readonly Backend _backend;
	
	public NetworkTimeHolder(Backend backend)
	{
		_backend = backend;
		_systemInitialTime = DateTime.UtcNow;
		_isFirstInit = true;
	}
	
	public bool IsServerReached { get => _networkInitialTime != default; }
	
	// This method is built only for validating timestamps acuired after a moment when:
	// 1) _systemInitialTime is initialized:
	// 2) there is a valid value for _networkInitialTime.
	// This happens when GetInitialTimeAsync was invoked and returned a valid result.
	// Validating events from previous sessions won't bring any result.
	public DateTime GetVerifiedTime(DateTime timestamp) 
	{
		var timePassedSinceInit = timestamp - _systemInitialTime;
		if (timePassedSinceInit < 0 || _networkInitialTime == default)
			return timestamp;
		else
			return _networkInitialTime.AddSeconds((timestamp - _systemInitialTime).TotalSeconds);
	}		
	
	public async UniTask<(bool, DateTime)> GetInitialTimeAsync(CancellationToken token) 
	{
		// bool isWaitingCancelled = await UniTask.WaitUntil(
			// () => {
				// Debug.LogWarning($"[ADVANAL] Wait until _isLoopRunning = false. _isLoopRunning = {_isLoopRunning}");
				// return _isLoopRunning == false; }, 
			// PlayerLoopTiming.PostLateUpdate,
			// token)
				// .SuppressCancellationThrow();
				
		// if (isWaitingCancelled) return (isWaitingCancelled, default(DateTime));
		
		var timeSincePrevInit = DateTime.UtcNow - _systemInitialTime;
		//Debug.LogWarning($"[ADVANAL] timeSincePrevInit = {timeSincePrevInit}");
		if (_networkInitialTime != default && Math.Abs(timeSincePrevInit.TotalSeconds) <= 1) 
		{
			//Debug.LogWarning("[ADVANAL] There is too little time since prev init, ignore GetInitialTimeAsync's web request");
			return (false, _networkInitialTime);
		}
		
		_networkInitialTime = default;
		_systemInitialTime = _isFirstInit? _systemInitialTime : DateTime.UtcNow;
		_isFirstInit = false;
		while (true)
        {
#if UNITY_EDITOR
			if (!Application.isPlaying)
				return default;
#endif		
			_isLoopRunning = true;
			//Debug.LogWarning("[ADVANAL] tAttempt to get network time...");
			var (isRequestCancelled, currentNetworkTime) = await _backend.GetNetworkTime(token);
			
			if (isRequestCancelled) return (isRequestCancelled, default(DateTime));
			
			//Debug.LogWarning($"[ADVANAL] currentNetworkTime = {currentNetworkTime}");
            if (currentNetworkTime == default)
            {
				//Debug.LogWarning("[ADVANAL] Time synchronization failed. Waiting for the next attempt...");
				bool isDelayingCancelled = await UniTask.Delay(TimeSpan.FromMinutes(2), 
														   false, 
														   PlayerLoopTiming.PostLateUpdate,
														   token)
					.SuppressCancellationThrow();
					
				if (isDelayingCancelled) return (isDelayingCancelled, default(DateTime));
            }
            else
            {
				//Debug.LogWarning($"[ADVANAL] _networkInitialTime.AddTicks((currentNetworkTime - (DateTime.UtcNow - _systemInitialTime)).Ticks) = {_networkInitialTime}.AddTicks(({currentNetworkTime} - ({DateTime.UtcNow} - {_systemInitialTime})).Ticks");
				_networkInitialTime = _networkInitialTime.AddTicks((currentNetworkTime - (DateTime.UtcNow - _systemInitialTime)).Ticks);
				//Debug.LogWarning("[ADVANAL] network initial time: " + _networkInitialTime);
				break;
            }
        }
		_isLoopRunning = false;
		return (false, _networkInitialTime);
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
		//Debug.LogWarning($"[ADVANT] The app is losing focus - validating GameEventsPool");
		for (int i = 0; i < pool.GetCurrentBusyCount(); ++i)
		{
			ValidateTimestamps(ref pool.Elements[i]);
		}
	}
	
	public void ValidateTimestamps(GameSessionsPool pool)
	{
		//Debug.LogWarning($"[ADVANT] The app is losing focus - validating GameSessionsPool");
		if (pool.HasCurrentSession() && IsServerReached)
			ValidateTimestamps(ref pool.CurrentSession());
	}
	
	public void ValidateTimestamps(ref GameProperty p)
	{
		// ValidateTimestamps(ref p.GetValue());
	}
	
	public void ValidateTimestamps(ref Session s)
	{
		if (s.HasValidTimestamps || !IsServerReached) return;
		
		try 
		{
			s.LastValidTimestamp = s.LastActivity = GetVerifiedTime(s.LastActivity);
			s.HasValidTimestamps = true;
			//Debug.LogWarning($"[ADVANT] validated session {s.SessionCount} timestamp = {s.LastActivity}, HasValidTimestamps = {s.HasValidTimestamps}");
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"[ADVANT] Error while validating Session timestamp. Session number: {s.SessionCount}, error message: {ex.Message}");
		}
	}
	
	public void ValidateTimestamps(ref GameEvent e)
	{
		if (e.HasValidTimestamps || !IsServerReached) return;
		
		try 
		{
			e.Time = GetVerifiedTime(e.Time);
			for (int i = 0; i < e.ParamsCount; ++i)
			{
				ValidateTimestamps(ref e.Params[i]);
			}
			e.HasValidTimestamps = true;
			//Debug.LogWarning($"[ADVANT] validated event {e.Name} timestamp = {e.Time}, HasValidTimestamps = {e.HasValidTimestamps}");
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"[ADVANT] Error while validating GameEvent timestamp. Event name: {e.Name}, error message: {ex.Message}");
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
