using System;
using Cysharp.Threading.Tasks;

internal static class RealDateTime
{
	public static DateTime UtcNow
	{ 
		get => _isSystemTimeDifferent && _DateTime.UtcNow > systemInitialTime ? 
			_networkInitialTime.AddSeconds((_DateTime.UtcNow - systemInitialTime).TotalSeconds) :
			DateTime.UtcNow; 
		private set; 
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
	
	public static async UniTask SychronizeTime()
	{
		_networkInitialTime = await _backend.GetNetworkTIme();
		_systemInitialTime = DateTime.UtcNow;
		
		if (_networkInitialTime - _systemInitialTime > TimeSpan.FromSeconds(10))
			_isSystemTimeDifferent = true;
	}
}