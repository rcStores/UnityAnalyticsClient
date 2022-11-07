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

		private const int 	GET_ID_RETRY_INTERVAL 		= 15000;
		private const int 	GET_COUNTRY_RETRY_INTERVAL 	= 150000; // 2.5 min
        private const string USER_ID_PREF 				= "UserId";
        private const string APP_VERSION_PREF 			= "AppVersion";

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

        public async UniTask<long> RegistrateAsync(RegistrationToken token) 
        {
			long result;
            while (await _backend.GetOrCreateUserIdAsync(token) is var response)
            {
#if UNITY_EDITOR
				if (!Application.isPlaying)
					return 0;
#endif				
                if (response.UserId == -1)
                {
					Debug.LogWarning("[ADVANAL] User registration failed");
					await UniTask.Delay(
						GET_ID_RETRY_INTERVAL, 
						false, 
						PlayerLoopTiming.PostLateUpdate);
                    Log.Info("retry");
					Debug.LogWarning("[ADVANAL] Retry user registration");
                }
                else
                {
                    _userId = response.UserId;
                    PlayerPrefs.SetInt(USER_ID_PREF, Convert.ToInt32(_userId));
                    _sessionCount = result = response.SessionCount;
					break;
                }
            }
			
			_country = await GetCountryAsync(0, 1);
			Debug.LogWarning($"[ADVANAL] SessionCount = {result}, country = {_country}");
			Debug.LogWarning($"[ADVANAL] UserId = {_userId}");
            Log.Info("Success. Start sending task");
			
            return result;
        }

        public long 	GetUserId() 	=> _userId;
		public bool 	IsTester() 		=> _isTester;
		public bool		IsCheater()		=> _isCheater;
		public string 	GetCountry()	=> _country;
		
		public void SetCheater(bool isCheater) => _isCheater = isCheater;
		
		public async UniTask<string> GetCountryAsync(int timeout, int attemptsCount = -1) 
		{
			if (string.IsNullOrEmpty(_country))
			{
				while (attemptsCount != 0)
				{
					_country = await _backend.GetCountryAsync(timeout);
					attemptsCount--;
					if (string.IsNullOrEmpty(_country) && attemptsCount != 0)
					{
						await UniTask.Delay(
							GET_COUNTRY_RETRY_INTERVAL, 
							false, 
							PlayerLoopTiming.PostLateUpdate);		
					}
					else break;
				}
			}
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