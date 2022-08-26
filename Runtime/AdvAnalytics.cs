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
		
		private const string CUSTOM_PROPERTIES_TABLE 	= "custom_properties";
		private const string USERS_DATA_TABLE 			= "users";

        private const string APP_VERSION_PREF 	= "AppVersion";
        private const string USER_ID_PREF 		= "UserId";

        static AdvAnalytics()
        {
            _backend 			= new Backend();
            _cacheHolder 		= new CacheScheduledHolder(USERS_DATA_TABLE, _backend);
            _userRegistrator 	= new UserRegistrator(USERS_DATA_TABLE, _backend);
        }

        public static void Init(string endpointsPathBase)
        {
            _backend.SetPathBase(endpointsPathBase);

            string idfv = SystemInfo.deviceUniqueIdentifier;
            Log.Info("Handling IDs");
			
// ---------------------------------------------------------------------------------------------
#if UNITY_EDITOR && DEBUG_ANAL
            InitImplAsync(new Identifier(platform: "IOS", "DEBUG", "DEBUG"));
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
                InitImplAsync(new Identifier(platform: "Android", idfv, gaid));
            });
// ---------------------------------------------------------------------------------------------
#elif UNITY_IOS
            InitImplAsync(new Identifier(platform: "IOS", idfv, Device.advertisingIdentifier));
#endif
        }
		
		public static ref GameEvent NewEvent(string eventName) 			=> ref _cacheHolder.NewEvent(eventName);
		
		public static void SendProperty(string name, int value)			=> _cacheHolder.NewProperty(name, value, CUSTOM_PROPERTIES_TABLE);		
		public static void SendProperty(string name, double value)		=> _cacheHolder.NewProperty(name, value, CUSTOM_PROPERTIES_TABLE);		
		public static void SendProperty(string name, bool value)		=> _cacheHolder.NewProperty(name, value, CUSTOM_PROPERTIES_TABLE);	
		public static void SendProperty(string name, DateTime value)	=> _cacheHolder.NewProperty(name, value, CUSTOM_PROPERTIES_TABLE);		
		public static void SendProperty(string name, string value)		=> _cacheHolder.NewProperty(name, value, CUSTOM_PROPERTIES_TABLE);

        public static void SaveCacheLocally() 							=> _cacheHolder.SaveCacheLocally();
        
        public static void SetCheater(bool value)						=> _cacheHolder.NewProperty("cheater", value, CUSTOM_PROPERTIES_TABLE);

        public static bool GetTester() 									=> _userRegistrator.IsTester();

        private static async void InitImplAsync(Identifier id)
        {    
            SendUserDetails(await _userRegistrator.RegistrateAsync(id));
            _cacheHolder.StartSendingDataAsync(_userRegistrator.GetUserId());
        }
        
        private static void SendUserDetails(bool isUserNew)
        {
			_cacheHolder.NewEvent("logged_in");
			
            if (isUserNew)
            {
                Log.Info("Create properties for a new user");

				_cacheHolder.NewProperty("first_install_date", 		DateTime.UtcNow.ToUniversalTime(), 	USERS_DATA_TABLE);
				_cacheHolder.NewProperty("last_install_date", 		DateTime.UtcNow.ToUniversalTime(), 	USERS_DATA_TABLE);
				_cacheHolder.NewProperty("first_game_version", 		Application.version, 				USERS_DATA_TABLE);
				_cacheHolder.NewProperty("current_game_version", 	Application.version, 				USERS_DATA_TABLE);

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
			
			_cacheHolder.NewProperty("cheater", false, 																	USERS_DATA_TABLE);
			_cacheHolder.NewProperty("os", 		Application.platform == RuntimePlatform.Android ? "android" : "ios", 	USERS_DATA_TABLE);
        }	
    }
}
