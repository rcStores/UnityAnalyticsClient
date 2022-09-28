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
        private readonly string 			_userPropertiesTableName;
        private readonly Backend 			_backend;

        private long 	_userId;
		private long 	_sessionCount;
		private bool 	_isTester;
		private bool 	_isCheater;
		private string 	_country;

		private const int 	GET_ID_RETRY_INTERVAL 	= 15000;
        private const string USER_ID_PREF 			= "UserId";
        private const string APP_VERSION_PREF 		= "AppVersion";

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

        public async UniTask<long> RegistrateAsync(Identifier identifier) //OnStartAsync, add OnAwakeningAsync: session count only
        {
			long result;
            identifier.UserId = _userId;
            while (await _backend.GetOrCreateUserIdAsync(identifier) is var response)
            {
#if UNITY_EDITOR
				if (!Application.isPlaying)
					return 0;
#endif				
                if (response.UserId == -1)
                {
                    //await Task.Delay(GET_ID_RETRY_INTERVAL);
					await UniTask.Delay(
						GET_ID_RETRY_INTERVAL, 
						false, 
						PlayerLoopTiming.PostLateUpdate);
                    Log.Info("retry");
					Debug.LogWarning("[ADVANAL] User registration failed. Retry...");
                }
                else
                {
                    _userId = response.UserId;
                    PlayerPrefs.SetInt(USER_ID_PREF, Convert.ToInt32(_userId));
                    _sessionCount = result = response.SessionCount;
					break;
                }
            }
			var (_isTester, _country) = await UniTask.WhenAll(
				_backend.GetTester(_userId), 
				GetCountryAsync(0));
            Log.Info("Success. Start sending task");
			Debug.LogWarning("[ADVANAL] SessionCount = " + result);
            return result;
        }

        public long 	GetUserId() 	=> _userId;
		public bool 	IsTester() 		=> _isTester;
		public bool		IsCheater()		=> _isCheater;
		public string 	GetCountry() 	=> _country;
		
		public void SetCheater(bool isCheater) => _isCheater = isCheater;
		
		public async UniTask<string> GetCountryAsync(int timeout) 
		{
			if (_country == null)
				_country = await _backend.GetCountryAsync(timeout);
			return _country;
		}
		
		// public async UniTask<long> GetCurrentSessionCountAsync()
		// {
			// if (_sessionCount == 0)
				// _sessionCount = await _backend.GetCurrentSessionCount(_userId);
			// return _sessionCount;
		// }
    }
}