using Advant.Data;
using Advant.Data.Models;
using Advant.Http;
using Advant.Logging;
#if UNITY_ANDROID
using AndroidUtils;
#endif

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine; 
#if UNITY_IOS
using UnityEngine.iOS;
#endif

namespace Advant
{
    public static class AdvAnalytics 
    {
        private static readonly Backend 				_backend;
        private static readonly CacheScheduledHolder 	_cacheHolder;
        private static readonly UserRegistrator 		_userRegistrator;
		
		private const string CUSTOM_PROPERTIES_TABLE	= "custom_properties";
		private const string USERS_DATA_TABLE 			= "users";

        private const string APP_VERSION_PREF 			= "AppVersion";
        private const string USER_ID_PREF 				= "UserId";

        static AdvAnalytics()
        {
            _backend 			= new Backend();
            _cacheHolder 		= new CacheScheduledHolder(USERS_DATA_TABLE, _backend);
            _userRegistrator 	= new UserRegistrator(USERS_DATA_TABLE, _backend);
        }

        public static void StartInit(string analyticsPathBase, string registrationPathbase, string abMode)
        {
            _backend.SetPathBases(analyticsPathBase, registrationPathbase);

            string idfv = SystemInfo.deviceUniqueIdentifier;
            Log.Info("Handling IDs");
			
// ---------------------------------------------------------------------------------------------
#if UNITY_EDITOR && DEBUG_ANAL
            InitAsync(new Identifier(platform: "IOS", "DEBUG", "DEBUG"), abMode);
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
                InitAsync(new Identifier(platform: "Android", idfv, gaid), abMode);
            });
// ---------------------------------------------------------------------------------------------
#elif UNITY_IOS
            InitAsync(new Identifier(platform: "IOS", idfv, Device.advertisingIdentifier), abMode);
#endif
        }
		
		public static ref GameEvent NewEvent(string eventName,
											 params string[] globalsLookupSource) 
																		=> ref _cacheHolder.NewEvent(eventName, globalsLookupSource);
		public static ref GameEvent NewEvent(string eventName) 			=> ref _cacheHolder.NewEvent(eventName);																
		
		public static void SendProperty(string name, int value)			=> _cacheHolder.NewProperty(name, value, CUSTOM_PROPERTIES_TABLE);		
		public static void SendProperty(string name, double value)		=> _cacheHolder.NewProperty(name, value, CUSTOM_PROPERTIES_TABLE);		
		public static void SendProperty(string name, bool value)		=> _cacheHolder.NewProperty(name, value, CUSTOM_PROPERTIES_TABLE);	
		public static void SendProperty(string name, DateTime value)	=> _cacheHolder.NewProperty(name, value, CUSTOM_PROPERTIES_TABLE);		
		public static void SendProperty(string name, string value)		=> _cacheHolder.NewProperty(name, value, CUSTOM_PROPERTIES_TABLE);

        public static void SaveCacheLocally() 							=> _cacheHolder.SaveCacheLocally();
        
        public static void SetCheater(bool value) 
		{
			_userRegistrator.SetCheater(value);
			_cacheHolder.NewProperty("cheater", value, USERS_DATA_TABLE);
		}
		
		public static void SetTrafficSource(string source)				=> _cacheHolder.NewProperty("traffic", source, USERS_DATA_TABLE);

        public static bool GetTester() 									=> _userRegistrator.IsTester();
		
		// In seconds. Note: The set timeout may apply to each URL redirect on Android which can result in a longer response
		public static async Task<string> GetCountryAsync(int timeout = 0) 	=> await _userRegistrator.GetCountryAsync(timeout);
		
		public static void SetGlobalEventParam(string name, int value) 		=> _cacheHolder.SetGlobalEventParam(name, value);
		public static void SetGlobalEventParam(string name, double value)	=> _cacheHolder.SetGlobalEventParam(name, value);
		public static void SetGlobalEventParam(string name, bool value) 	=> _cacheHolder.SetGlobalEventParam(name, value);
		public static void SetGlobalEventParam(string name, DateTime value)	=> _cacheHolder.SetGlobalEventParam(name, value);
		public static void SetGlobalEventParam(string name, string value) 	=> _cacheHolder.SetGlobalEventParam(name, value);
		
		public static void SetCurrentArea(int area) 						=> _cacheHolder.SetCurrentArea(area);
		public static void SetCurrentAbMode(string mode) 					=> _cacheHolder.SetCurrentAbMode(mode, USERS_DATA_TABLE);
		
		private static async void InitAsync(Identifier id, string abMode)
        {
			_cacheHolder.NewEvent("logged_in");
			_cacheHolder.SetSessionStart();
			
            SendUserDetails(await _userRegistrator.RegistrateAsync(id), abMode);
            _cacheHolder.StartSendingDataAsync(_userRegistrator.GetUserId());
        }
        
        private static void SendUserDetails(long sessionCount, string abMode)
        {		
			_cacheHolder.SetSessionCount(sessionCount);
			
			if (sessionCount == 0) return;
			
            if (sessionCount == 1)
            {
                Log.Info("Create properties for a new user");
				
				if (!_userRegistrator.IsCheater())
					_cacheHolder.NewProperty("cheater", false, USERS_DATA_TABLE);
				
				_cacheHolder.NewProperty("tester", 					GetTester(), 						USERS_DATA_TABLE);
				_cacheHolder.NewProperty("first_install_date", 		DateTime.UtcNow.ToUniversalTime(), 	USERS_DATA_TABLE);
				_cacheHolder.NewProperty("last_install_date", 		DateTime.UtcNow.ToUniversalTime(), 	USERS_DATA_TABLE);
				_cacheHolder.NewProperty("first_game_version", 		Application.version, 				USERS_DATA_TABLE);
				_cacheHolder.NewProperty("current_game_version", 	Application.version, 				USERS_DATA_TABLE);
				_cacheHolder.NewProperty("first_ab_mode", 			abMode, 							CUSTOM_PROPERTIES_TABLE);

                PlayerPrefs.SetString(APP_VERSION_PREF, Application.version);
            }
            else
            {
                Log.Info("Create properties for registered user");
				
                string appVersion = PlayerPrefs.GetString(APP_VERSION_PREF);
                if (appVersion != "" && appVersion != Application.version)
                {
					_cacheHolder.NewProperty("last_update_date", DateTime.UtcNow.ToUniversalTime(), USERS_DATA_TABLE);
                }
                else if (appVersion == "")
                {
					_cacheHolder.NewProperty("last_install_date", DateTime.UtcNow.ToUniversalTime(), USERS_DATA_TABLE);
                }
				
				_cacheHolder.NewProperty("current_game_version", Application.version, USERS_DATA_TABLE);
				
                PlayerPrefs.SetString(APP_VERSION_PREF, Application.version);
            }
			_cacheHolder.NewProperty("os", Application.platform == RuntimePlatform.Android ? "android" : "ios", USERS_DATA_TABLE);
			_cacheHolder.NewProperty("country", _userRegistrator.GetCountry(), USERS_DATA_TABLE);
        }	
    }
}
