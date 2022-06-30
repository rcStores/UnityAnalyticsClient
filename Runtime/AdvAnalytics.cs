﻿using Advant.Data;
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
		
		public static void Init(string endpointsPathBase)
        {
            _backend.SetPathBase(endpointsPathBase);

            string idfv, idfa = null, platform = null;
            Logger.Log("Handling IDs");
#if UNITY_ANDROID
            AndroidGAIDRetriever.GetAsync((string gaid) => idfa = gaid);
            platform = "Android";          
#elif UNITY_IOS
            idfa = Device.advertisingIdentifier;
            platform = "IOS";
#endif
			if (idfa is null)
			{
				Logger.Log("IDFA could'nt be gotten");
			}
            idfv = SystemInfo.deviceUniqueIdentifier;
            SendEvent("logged_in");
            _cacheHolder.StartAsync(new Identifier(platform, idfv, idfa));
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