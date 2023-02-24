using Advant.Data;
using Advant.Data.Models;
using Advant.Http;
using Advant.Logging;
#if UNITY_ANDROID
using AndroidUtils;
#endif

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine; 
#if UNITY_IOS
using UnityEngine.iOS;
#endif

namespace Advant
{
    public static class AdvAnalytics 
    {
        private static readonly IHttpClient		_backend;
        private static readonly CacheScheduledHolder 	_cacheHolder;
        private static readonly UserRegistrator 		_userRegistrator;
		private static readonly NetworkTimeHolder 		_timeHolder;
		private static readonly DTDLogger 				_dtdLogger;
		
		private static CancellationTokenSource _networkTimeCTS;
		
		private const string CUSTOM_PROPERTIES_TABLE	= "custom_properties";
		private const string USERS_DATA_TABLE 			= "users";
        private const string ENTRY_PREF 				= "HasPreviousEntry";

        static AdvAnalytics()
        {
			_dtdLogger 			= new DTDLogger();
            _backend 			= new CppHttpClient();
			_timeHolder			= new NetworkTimeHolder(_backend);
            _cacheHolder 		= new CacheScheduledHolder(USERS_DATA_TABLE, _backend, _timeHolder);
            _userRegistrator 	= new UserRegistrator(USERS_DATA_TABLE, _backend);
			_networkTimeCTS 	= new CancellationTokenSource();
        }
		
		/// <summary>
		/// Serialize all analytics data to the game directory of user device. Call this method 
		/// This method is built for being called <see cref=OnApplicationFocus(bool)/> with false value.
		/// </summary>
		public static void SaveCacheLocally() 
		{	
			//Debug.LogWarning($"[ADVANT] AdvAnalytics.SaveCacheLocally");
			// the game is being minimized, so the current attempt of getting network time (if it is not finished yet)
			// will cause inadequate consequences when the app gets focus again
			_networkTimeCTS.Cancel();
			_networkTimeCTS = new CancellationTokenSource();
			_cacheHolder.SaveCacheLocally();
		}
		
		private static bool HasEntryPref() 	=> PlayerPrefs.GetInt(ENTRY_PREF, 0) != 0;
		private static void SaveEntryPref()	=> PlayerPrefs.SetInt(ENTRY_PREF, 1);
		
#region DTD Logging
		
		public static void LogFailureToDTD(string failure, Exception exception, Type advInnerType = null)
		{
			_dtdLogger?.LogFailure(failure, exception, advInnerType);
		}

		public static void LogMessageToDTD(string message)
		{
			_dtdLogger?.LogMessage(message);
		}

		public static void LogWebRequestToDTD(string requestName, 
												    bool isSuccess,
												    long statusCode,
												    string requestError,
												    string exception)
		{
			_dtdLogger?.LogWebRequest(requestName, isSuccess, statusCode, requestError, exception);
		}

		public static void LogDataSendingToDTD(string dataType,
													 int batchSize,
													 bool isSuccess,
													 long statusCode,
													 string requestError,
													 string exception,
													 string age)
		{
			_dtdLogger?.LogDataSending(dataType, batchSize, isSuccess, statusCode, requestError, exception, age);
		}
	
#endregion

#region Initialization
		/// <summary>
		/// This method initializes all helper classes of the tool and starts running a sending loop.
		/// Although there will be no data sending without calling this method, it doesn't affect collecting of data.
		/// </summary>
        public static void StartInit(string analyticsPathBase, 
									 string registrationPathbase, 
									 string abMode, 
									 Action<string> messageLogger, 
									 Action<string, Exception, Type> failureLogger, 
									 Action<string, bool, long, string, string> webRequestLogger,
									 Action<string, int, bool, long, string, string, string> dataSendingLogger)
        {
			_dtdLogger.InitDelegates(messageLogger, failureLogger, webRequestLogger, dataSendingLogger);
			
            _backend.SetPathBases(analyticsPathBase, registrationPathbase);

            string idfv = SystemInfo.deviceUniqueIdentifier;
            Log.Info("Handling IDs");
			
// ---------------------------------------------------------------------------------------------
#if UNITY_EDITOR && DEBUG_ANAL
            InitAsync(new RegistrationToken(platform: "IOS", 
											idfv: "DEBUG", 
											idfa: "DEBUG", 
											abMode, 
											Application.version,
											initializedBefore: HasEntryPref())).Forget();
// ---------------------------------------------------------------------------------------------			
#elif UNITY_EDITOR
			return;
// ---------------------------------------------------------------------------------------------			
#elif UNITY_ANDROID
            GAIDRetriever.GetAsync((string gaid) => {
                if (gaid is null)
			    {
				    Log.Info("GAID couldn't be received");
			    }
                InitAsync(new RegistrationToken(platform: "Android", 
												idfv: idfv, 
												idfa: gaid, 
												abMode, 
												Application.version,
												initializedBefore: HasEntryPref())).Forget();
            });
// ---------------------------------------------------------------------------------------------
#elif UNITY_IOS
            InitAsync(new RegistrationToken(platform: "IOS", 
											idfv: idfv, 
											idfa: Device.advertisingIdentifier, 
											abMode, 
											Application.version,
											initializedBefore: HasEntryPref())).Forget();
#endif
        }
		
		/// <summary>
		/// Wakes the tool after the app got the focus again. Must be called in<see cref=OnApplicationFocus(bool)/> with true value.
		/// </summary>
		public static async UniTaskVoid Refresh()
		{
			var (isCancelled, initialTime) = await _timeHolder.GetInitialTimeAsync(_networkTimeCTS.Token);
			if (!isCancelled)
				_cacheHolder.StartOrContinueSessionAsync(initialTime);
		}
				
		private static async UniTaskVoid InitAsync(RegistrationToken token)
        {
			var ((isGettingTimeCancelled, initialTime), dbSessionCount) = await UniTask.WhenAll(
				_timeHolder.GetInitialTimeAsync(_networkTimeCTS.Token),
				_userRegistrator.RegistrateAsync(token));
			
			_cacheHolder.SetUserId(_userRegistrator.GetUserId());
			if (!isGettingTimeCancelled)
				_cacheHolder.StartOrContinueSessionAsync(initialTime, dbSessionCount);
			
            PrepareClientForSendingAsync(); // now preparations doesn't have to be completed before starting the loop, so don't use await. this might change later
            _cacheHolder.StartSendingDataAsync();
        }
		
		// put in this method all actions that must be performed before running the sending loop
		private static async UniTaskVoid PrepareClientForSendingAsync()
        {
			SaveEntryPref();
			
			_cacheHolder.NewProperty("country", await _userRegistrator.GetCountryAsync(timeout: 0), USERS_DATA_TABLE);
			if (!_userRegistrator.IsCheater())
				_cacheHolder.NewProperty("cheater", false, USERS_DATA_TABLE);
        }

#endregion		

#region Analytic data sending
		
		/// <summary>
		/// Extracts a new game event entry from the pool (allocation-free) and puts it into the sending queue implicitly. 
		/// This method alone is enough to send the property to the server (if the tool was initialized), but the element is still acessible for initialization for some time (look below).
		/// </summary>
		/// <param name="globalsLookupSource">
		/// Names of all parameters of the game event. 
		/// Passed in cases where there is global parameters being passed to each event implicitly. Look at <see cref=SetGlobalEventParam(string, int)/>.
		/// </param>
		/// <returns>
		/// The reference to the exctracted element. It can still be accessible up to LateUpdate of a current game loop's iteration. For more info look at GameEvent.Add().
		/// </returns>
		public static ref GameEvent NewEvent(string eventName,
											 params string[] globalsLookupSource) 
																		=> ref _cacheHolder.NewEvent(eventName, globalsLookupSource);
		
		/// <overload> <see cref=NewEvent(string, params string[])/> </overload>	
		public static ref GameEvent NewEvent(string eventName) 			=> ref _cacheHolder.NewEvent(eventName);																
		
		/// <summary>
		/// Extracts a new game property entry from the pool (allocation-free) and puts it into the sending queue implicitly. 
		/// This method alone is enough to send the property to the server (if the tool was initialized).
		/// All created properties will be considered as custom ones.
		/// </summary>
		public static void SendProperty(string name, int value)			=> _cacheHolder.NewProperty(name, value, CUSTOM_PROPERTIES_TABLE);
		/// <overload> <see cref="SendProperty(string, int)"/> </overload>	
		public static void SendProperty(string name, double value)		=> _cacheHolder.NewProperty(name, value, CUSTOM_PROPERTIES_TABLE);
		/// <overload> <see cref="SendProperty(string, int)"/> </overload>			
		public static void SendProperty(string name, bool value)		=> _cacheHolder.NewProperty(name, value, CUSTOM_PROPERTIES_TABLE);	
		/// <overload> <see cref="SendProperty(string, int)"/> </overload>	
		public static void SendProperty(string name, string value)		=> _cacheHolder.NewProperty(name, value, CUSTOM_PROPERTIES_TABLE);
		
#endregion

#region Setters
		
		/// <summary>
		/// Sets the property value for the users' table in the dataset. Makes the analytic SDK remember the value for the app lifetime.
		/// </summary>
		public static void SetCheater(bool value) 
		{
			_userRegistrator.SetCheater(value);
			_cacheHolder.NewProperty("cheater", value, USERS_DATA_TABLE);
		}
		
		/// <summary>
		/// Sets the property value for the users' table in the dataset.
		/// </summary>
		public static void SetTrafficSource(string source)	=> _cacheHolder.NewProperty("traffic", source, USERS_DATA_TABLE);
		
		/// <summary>
		/// Sets the property value for the users' table in the dataset.
		/// </summary>
		public static void SetGameLang(string lang)			=> _cacheHolder.NewProperty("language", lang, USERS_DATA_TABLE);
		
		/// <summary>
		/// The tool's design implies the current <paramref name="area"> value being used in next places:
		/// 1) as a current session parameter;
		/// 2) as a global events parameter (see <see cref="SetGlobalEventParam(string, int)"/>).
		/// 
		/// So the method bounds sets the value for both.
		/// </summary>
		public static void SetCurrentArea(int area) 		=> _cacheHolder.SetCurrentArea(area);
		
		/// <summary>
		/// The tool's design implies the current <paramref name="mode"> value being used in next places:
		/// 1) as a current session parameter;
		/// 2) as a global events parameter (see <see cref="SetGlobalEventParam(string, int)"/>);
		/// 3) as a custom property;
		/// 
		/// So the method bounds sets the value for both.
		/// </summary>
		public static void SetCurrentAbMode(string mode) 	=> _cacheHolder.SetCurrentAbMode(mode, CUSTOM_PROPERTIES_TABLE);
		
		/// <summary>
		/// Makes the <paramref name="name"/>-<paramref name="value"> pair appear as an additional parameter in all game events being sent after the call of this method.
		/// All repeated calls of this method's overloads having the same <paramref name="name"/> will overwrite the parameter <paramref name="value">.
		/// Once whis method is called, one should be careful with duplication of parameters passed to the event with <see cref=GameEvent.Add(string, int)/>.
		/// </summary>
		public static void SetGlobalEventParam(string name, int value) 		=> _cacheHolder.SetGlobalEventParam(name, value);
		/// <overload> <see cref="SetGlobalEventParam(string, int)"/> </overload>
		public static void SetGlobalEventParam(string name, double value)	=> _cacheHolder.SetGlobalEventParam(name, value);
		/// <overload> <see cref="SetGlobalEventParam(string, int)"/> </overload>
		public static void SetGlobalEventParam(string name, bool value) 	=> _cacheHolder.SetGlobalEventParam(name, value);
		/// <overload> <see cref="SetGlobalEventParam(string, int)"/> </overload>
		public static void SetGlobalEventParam(string name, DateTime value)	=> _cacheHolder.SetGlobalEventParam(name, value);
		/// <overload> <see cref="SetGlobalEventParam(string, int)"/> </overload>
		public static void SetGlobalEventParam(string name, string value) 	=> _cacheHolder.SetGlobalEventParam(name, value);

#endregion

#region Getters
		
		/// <summary>
		/// Provokes making a GET-request to the external service. This method can be used independently on analytics being initialized.
		/// </summary>
		/// <param name="timeout">
		/// Timeout value in seconds. Default value results in no timeout.
		/// Note: The set timeout may apply to each URL redirect on Android which can result in a longer response.
		/// </param>
		public static async UniTask<string> 	GetCountryAsync(int timeout = 0)	=> await _userRegistrator.GetCountryAsync(timeout);
		
		/// <summary>
		/// Provokes making a GET-request to the internal network. 
		/// Since this requires users get registered in the system, this method can be called only after <see cref="StartInit(string, string, string)"/> done initializing.
		/// </summary>
        public static 		bool 				GetTester()							=> _userRegistrator.IsTester();
		
		/// <returns>
		/// Session number. After successful registration it's the number of the current session. Before it - the previous session's one.
		/// If no session was created yet (i.e. on first application start or when a cached data wasn't loaded from the disk) the method returns -1.
		/// </returns>
		public static 		long				GetSessionCount()					=> _cacheHolder.GetSessionCount();
		
		/// <summary>
		/// Provokes making a GET-request to the internal network. This method doesn't need a user being registered so, it can be used independently on analytics being initialized.
		/// </summary>
		/// <param name="timeout">
		/// Timeout value in seconds. Default value results in no timeout.
		/// Note: The set timeout may apply to each URL redirect on Android which can result in a longer response.
		/// <returns>
		/// Current network time. If the task was cancelled, the return value equals to <see cref="default(DateTime)"/>.
		/// </returns>
		public static async UniTask<DateTime> GetNetworkTimeAsync(CancellationToken token, int timeout = 0)
		{
			var (isCancelled, result) = await _backend.GetNetworkTime(token, timeout);
			return isCancelled ? default : result;
		}

#endregion
	}
}
