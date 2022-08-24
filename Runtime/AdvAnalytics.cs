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
        private static readonly Backend _backend;
        private static readonly CacheScheduledHolder _cacheHolder;

        private static readonly UserRegistrator _userRegistrator;
		
		private const string CUSTOM_PROPERTIES_TABLE = "custom_properties";
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
            InitImplAsync(new Identifier(platform: "IOS", "DEBUG", "DEBUG"));
			
#elif UNITY_EDITOR
			return;
			
#elif UNITY_ANDROID
            GAIDRetriever.GetAsync((string gaid) => {
                if (gaid is null)
			    {
				    Log.Info("GAID couldn't be received");
			    }
                InitImplAsync(new Identifier(platform: "Android", idfv, gaid));
            });
#elif UNITY_IOS
            InitImplAsync(new Identifier(platform: "IOS", idfv, Device.advertisingIdentifier));
#endif
        }
		
		public static ref GameEvent NewEvent(out int idx)
		{
			return ref _cacheHolder.NewEvent(out idx);
		}
		
		// public void SendEvent(int idx)
		// {
			// _cacheHolder.SendEvent(idx);
		// }
		
		// public ref GameProperty NewProperty(out int idx)
		// {
			// return ref _cacheHolder.NewProperty(idx).SetTableName(CUSTOM_PROPERTIES_TABLE);
		// }
		private static void SendPropertyImpl(string name, int value, string tableName)
		{
			ref var p = ref _cacheHolder.NewProperty(out var _);
			p.Set(name, value);
			p.SetTableName(tableName);
		}
		
		private static void SendPropertyImpl(string name, double value, string tableName)
		{
			ref var p = ref _cacheHolder.NewProperty(out var _);
			p.Set(name, value);
			p.SetTableName(tableName);
		}
		
		private static void SendPropertyImpl(string name, string value, string tableName)
		{
			ref var p = ref _cacheHolder.NewProperty(out var _);
			p.Set(name, value);
			p.SetTableName(tableName);
		}
		
		private static void SendPropertyImpl(string name, bool value, string tableName)
		{
			ref var p = ref _cacheHolder.NewProperty(out var _);
			p.Set(name, value);
			p.SetTableName(tableName);
		}
		
		private static void SendPropertyImpl(string name, DateTime value, string tableName)
		{
			ref var p = ref _cacheHolder.NewProperty(out var _);
			p.Set(name, value);
			p.SetTableName(tableName);
		}
		
		public static void SendProperty(string name, int value)
		{
			SendPropertyImpl(name, value, CUSTOM_PROPERTIES_TABLE);
		}
		
		public static void SendProperty(string name, double value)
		{
			SendPropertyImpl(name, value, CUSTOM_PROPERTIES_TABLE);
		}
		
		public static void SendProperty(string name, bool value)
		{
			SendPropertyImpl(name, value, CUSTOM_PROPERTIES_TABLE);
		}
		
		public static void SendProperty(string name, DateTime value)
		{
			SendPropertyImpl(name, value, CUSTOM_PROPERTIES_TABLE);
		}
		
		public static void SendProperty(string name, string value)
		{
			SendPropertyImpl(name, value, CUSTOM_PROPERTIES_TABLE);
		}

        public static void SaveCacheLocally()
        {
            _cacheHolder.SaveCacheLocally();
        }
        
        // public static void SendProperty<T>(string name, T value)
        // {
            // _cacheHolder.Put(GameProperty.Create(CUSTOM_PROPERTIES_TABLE, name, value));
        // }
		
		// public static void SendEvent(string name, Dictionary<string, object> parameters = null)
        // {
            // _cacheHolder.Put(new GameEvent(name, DateTime.UtcNow.ToUniversalTime(), Application.version, parameters));
        // }

        public static void SetCheater(bool value)
        {
            //_cacheHolder.Put(GameProperty.Create(USERS_DATA_TABLE, "cheater", value));
			_cacheHolder.NewProperty(out var _).Set("cheater", value).SetTableName(USERS_DATA_TABLE);
        }

        public static bool GetTester()
        {
            return _userRegistrator.IsTester();
        }

        private static async void InitImplAsync(Identifier id)
        {
            NewEvent(out var _).SetName("logged_in");
            SendUserDetails(await _userRegistrator.RegistrateAsync(id));
            _cacheHolder.StartSendingDataAsync(_userRegistrator.GetUserId());
        }
        
        private static void SendUserDetails(bool isUserNew)
        {
            if (isUserNew)
            {
                Log.Info("Create properties for a new user");
                // _cacheHolder.Put(GameProperty.Create(USERS_DATA_TABLE, "first_install_date", DateTime.UtcNow.ToUniversalTime()));
                // _cacheHolder.Put(GameProperty.Create(USERS_DATA_TABLE, "last_install_date", DateTime.UtcNow.ToUniversalTime()));
				// _cacheHolder.Put(GameProperty.Create(USERS_DATA_TABLE, "first_game_version", Application.version));
                // _cacheHolder.Put(GameProperty.Create(USERS_DATA_TABLE, "current_game_version", Application.version));
				SendPropertyImpl("first_install_date", DateTime.UtcNow.ToUniversalTime(), USERS_DATA_TABLE);
				SendPropertyImpl("last_install_date", DateTime.UtcNow.ToUniversalTime(), USERS_DATA_TABLE);
				SendPropertyImpl("first_game_version", Application.version, USERS_DATA_TABLE);
				SendPropertyImpl("current_game_version", Application.version, USERS_DATA_TABLE);

                PlayerPrefs.SetString(APP_VERSION_PREF, Application.version);
            }
            else
            {
                Log.Info("Create properties for registered user");
                string appVersion = PlayerPrefs.GetString(APP_VERSION_PREF);
                if (appVersion != "" && appVersion != Application.version)
                {
                    //_cacheHolder.Put(GameProperty.Create(USERS_DATA_TABLE, "last_update_date", DateTime.UtcNow.ToUniversalTime()));
					SendPropertyImpl("last_update_date", DateTime.UtcNow.ToUniversalTime(), USERS_DATA_TABLE);
                }
                else if (appVersion == "")
                {
                    //_cacheHolder.Put(GameProperty.Create(USERS_DATA_TABLE, "last_install_date", DateTime.UtcNow.ToUniversalTime()));
					SendPropertyImpl("last_install_date", DateTime.UtcNow.ToUniversalTime(), USERS_DATA_TABLE);
                }
                //_cacheHolder.Put(GameProperty.Create(USERS_DATA_TABLE, "current_game_version", Application.version));
				SendPropertyImpl("current_game_version", Application.version, USERS_DATA_TABLE);
                PlayerPrefs.SetString(APP_VERSION_PREF, Application.version);
            }

            //_cacheHolder.Put(GameProperty.Create(USERS_DATA_TABLE, "cheater", false));	
			// Debug.LogWarning($"USER {_userRegistrator.GetUserId()} is " + (GetTester() ? "tester" : "NOT tester"));
            //_cacheHolder.Put(GameProperty.Create("country", value));
            // _cacheHolder.Put(GameProperty.Create(
				// USERS_DATA_TABLE,
				// "os", 
				// SystemInfo.operatingSystem.Contains("Android") ? "android" : "ios"));
			
			SendPropertyImpl("cheater", false, USERS_DATA_TABLE);
			SendPropertyImpl("os", Application.platform == RuntimePlatform.Android ? "android" : "ios", USERS_DATA_TABLE);

        }
    }
}
