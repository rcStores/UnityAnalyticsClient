using Advant.Data;
using Advant.Data.Models;
using Advant.Http;
using Advant.Logging;
#if UNITY_ANDROID
using AndroidUtils;
#endif

using System;
using System.Collections.Generic;
using UnityEngine; 
#if UNITY_IOS
using UnityEngine.iOS;
#endif

namespace Advant
{
    public static class AdvAnalytics 
    {
        private static readonly Backend _backend;
        private static readonly CacheScheduledHolder _cacheHolder;

        private static readonly UserRegistrator _userRegistrator;
		
		private const string CUSTOM_PROPERTIES_TABLE = "custom_properies";
		private const string USERS_DATA_TABLE = "users";

        private const string APP_VERSION_PREF = "AppVersion";
        private const string USER_ID_PREF = "UserId";

        static AdvAnalytics()
        {
            _backend = new Backend();
            _cacheHolder = new CacheScheduledHolder(USERS_DATA_TABLE, _backend);
            _userRegistrator = new UserRegistrator(USERS_DATA_TABLE, _backend);
        }

        public static void Init(string endpointsPathBase)
        {
            _backend.SetPathBase(endpointsPathBase);

            string idfv = SystemInfo.deviceUniqueIdentifier;
            Log.Info("Handling IDs");

#if UNITY_EDITOR && DEBUG_ANAL
			Debug.LogWarning("[EDITOR] AdvAnalytics.Init()");
            InitImplAsync(new Identifier(platform: "IOS", "DEBUG", "DEBUG"));
			
#elif UNITY_EDITOR
			Debug.LogWarning("[EDITOR w/o ANALYTIC DEBUG] AdvAnalytics.Init()");
			return;
			
#elif UNITY_ANDROID
            Debug.LogWarning("[ANDROID] AdvAnalytics.Init()");
            GAIDRetriever.GetAsync((string gaid) => {
                if (gaid is null)
			    {
				    Log.Info("GAID couldn't be received");
			    }
                InitImplAsync(new Identifier(platform: "Android", idfv, gaid));
            });
#elif UNITY_IOS
			Debug.LogWarning("[IOS] AdvAnalytics.Init()");
            InitImplAsync(new Identifier(platform: "IOS", idfv, Device.advertisingIdentifier));
#endif
        }

        public static void SaveCacheLocally()
        {
            _cacheHolder.SaveCacheLocally();
        }
        
        public static void SendProperty<T>(string name, T value)
        {
            _cacheHolder.Put(GameProperty.Create(CUSTOM_PROPERTIES_TABLE, name, value));
        }
		
		public static void SendEvent(string name, Dictionary<string, object> parameters = null)
        {
            _cacheHolder.Put(new GameEvent(name, DateTime.UtcNow.ToUniversalTime(), Application.version, parameters));
        }

        public static void SetCheater(bool value)
        {
            _cacheHolder.Put(GameProperty.Create(USERS_DATA_TABLE, "cheater", value));
        }

        public static void GetTester()
        {
            //return _backend.GetTester(_cahceScheduler.GetUserId());
        }

        private static async void InitImplAsync(Identifier id)
        {
            SendEvent("logged_in");
            SendUserDetails(await _userRegistrator.RegistrateAsync(id));
            _cacheHolder.StartSendingDataAsync(_userRegistrator.GetUserId());
        }
        
        private static void SendUserDetails(bool isUserNew)
        {
            Debug.LogWarning("APP_VERSION = " + Application.version);
            if (isUserNew)
            {
                Debug.Log("Create properties for a new user");
                _cacheHolder.Put(GameProperty.Create(USERS_DATA_TABLE, "first_install_date", DateTime.UtcNow.ToUniversalTime()));
                _cacheHolder.Put(GameProperty.Create(USERS_DATA_TABLE, "last_install_date", DateTime.UtcNow.ToUniversalTime()));
                _cacheHolder.Put(GameProperty.Create(USERS_DATA_TABLE, "current_game_vers", Application.version));
                PlayerPrefs.SetString(APP_VERSION_PREF, Application.version);
            }
            else
            {
                Debug.LogWarning("Create properties for registered user");
                string appVersion = PlayerPrefs.GetString(APP_VERSION_PREF);
                Debug.LogWarning("Cached app version: " + appVersion);
                if (appVersion != "" && appVersion != Application.version)
                {
                    _cacheHolder.Put(GameProperty.Create(USERS_DATA_TABLE, "last_update_date", DateTime.UtcNow.ToUniversalTime()));
                }
                else if (appVersion == "")
                {
                    _cacheHolder.Put(GameProperty.Create(USERS_DATA_TABLE, "last_install_date", DateTime.UtcNow.ToUniversalTime()));
                }
                _cacheHolder.Put(GameProperty.Create(USERS_DATA_TABLE, "current_game_vers", Application.version));
                PlayerPrefs.SetString(APP_VERSION_PREF, Application.version);
            }

            Debug.LogWarning("Send user details: " + SystemInfo.operatingSystem);
            _cacheHolder.Put(GameProperty.Create(USERS_DATA_TABLE, "cheater", false));
            _cacheHolder.Put(GameProperty.Create(USERS_DATA_TABLE, "tester", false));
            //_cacheHolder.Put(GameProperty.Create("country", value));
            _cacheHolder.Put(GameProperty.Create(
				USERS_DATA_TABLE,
				"os", 
				SystemInfo.operatingSystem.Contains("Android") ? "android" : "ios"));

        }
    }
}
