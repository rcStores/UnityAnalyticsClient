using Advant.Data.Models;
using Advant.Http;
using Advant.Logging;
using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace Advant
{
    internal class UserRegistrator
    {
        private readonly string _userPropertiesTableName;
        private readonly Backend _backend;

        private const int GET_ID_RETRY_INTERVAL = 15000;
		
        private long _userId;
		private bool _isTester;
		private string _country;

        private const string USER_ID_PREF = "UserId";
        private const string APP_VERSION_PREF = "AppVersion";

        public UserRegistrator(string userPropertiesTableName, Backend backend)
        {
            string serializationPath = null;
#if UNITY_IOS || UNITY_EDITOR
            serializationPath = Path.Combine(Application.persistentDataPath, "CachedData");
#else
            serializationPath = Path.Combine(AndroidUtils.ApiUtil.GetPersistentDataPath(), "CachedData");
#endif
            _userPropertiesTableName = userPropertiesTableName;
            _backend = backend;
			_userId = Convert.ToInt64(PlayerPrefs.GetInt(USER_ID_PREF, -1));
        }

        public async UniTask<bool> RegistrateAsync(Identifier identifier)
        {
			bool result = false;
            identifier.UserId = _userId;
            while (await _backend.GetOrCreateUserIdAsync(identifier) is var response)
            {
#if UNITY_EDITOR
				if (!Application.isPlaying)
					return false;
#endif				
                if (response.UserId == -1)
                {
                    //await Task.Delay(GET_ID_RETRY_INTERVAL);
					await UniTask.Delay(
						GET_ID_RETRY_INTERVAL, 
						false, 
						PlayerLoopTiming.PostLateUpdate);
                    Log.Info("retry");
                }
                else
                {
                    _userId = response.UserId;
                    PlayerPrefs.SetInt(USER_ID_PREF, Convert.ToInt32(_userId));
                    result = response.IsUserNew;
					break;
                }
            }
			_isTester = await _backend.GetTester(_userId);
			_country = await _backend.GetCountry();
            Log.Info("Success. Start sending task");
            return result;;
        }

        public long GetUserId()
        {
            return _userId;
        }
		
		public bool IsTester()
		{
			return _isTester;
		}
		
		public string GetCountry()
		{
			return _country;
		}
    }
}