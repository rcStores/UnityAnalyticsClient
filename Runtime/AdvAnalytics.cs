using Advant.Data;
using Advant.Data.Models;
using Advant.Http;
using Advant.Logging;
using RosUtils;

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

        static AdvAnalytics()
        {
            _backend = new Backend();
            _cacheHolder = new CacheScheduledHolder(_backend);
        }
		
		private static void InitImpl(Identifier id)
		{
            SendEvent("logged_in");
            _cacheHolder.StartAsync(id);
		}
		
		public static void Init(string endpointsPathBase)
        {
            _backend.SetPathBase(endpointsPathBase);

            string idfv = SystemInfo.deviceUniqueIdentifier;
            Log.Info("Handling IDs");

#if UNITY_EDITOR && DEBUG_ANAL
			Debug.Log("DEBUG_ANAL");
			if (!Application.isPlaying) return;
            InitImpl(new Identifier(platform: "Android", "DEBUG", "DEBUG"));
			
#elif UNITY_EDITOR
			Debug.Log("RETURN FROM INIT");
			return;
			
#elif UNITY_ANDROID
            AndroidGAIDRetriever.GetAsync((string gaid) => {
                if (gaid is null)
			    {
				    Log.Info("GAID couldn't be received");
			    }
                InitImpl(new Identifier(platform: "Android", idfv, gaid));
            });
#elif UNITY_IOS
            InitImpl(new Identifier(platform: "IOS", idfv, Device.advertisingIdentifier));
#endif
        }

        static public void SaveCacheLocally()
        {
            _cacheHolder.SaveCacheLocally();
        }
        
        public static void SendProperty<T>(string name, T value)
        {
            _cacheHolder.Put(GameProperty.Create(name, value));
        }

        public static void SendEvent(string name, Dictionary<string, object> parameters = null)
        {
            _cacheHolder.Put(new GameEvent(name, DateTime.UtcNow.ToUniversalTime(), Application.version, parameters));
        }

        public static void SetCheater(bool value)
        {
            _cacheHolder.Put(GameProperty.Create("cheater", value));
        }

        public static void GetTester()
        {
            //return _backend.GetTester(_cahceScheduler.GetUserId());
        }
    }
}
