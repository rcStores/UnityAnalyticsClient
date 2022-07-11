using Advant.Data.Models;
using Advant.Http;
using Advant.Logging;
using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace Advant
{
    internal class UserRegistrator
    {
        private readonly string _userPropertiesTableName;
        private readonly Backend _backend;

        private const int GET_ID_RETRY_INTERVAL = 15000;

        private Func<long, Task> _onRegistrationSuccess;
        private long _userId;

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

        public UserRegistrator SetActionOnSuccess(Func<long, Task> func)
        {
            _onRegistrationSuccess = func;
            return this;
        }

        public async Task<bool> RegistrateAsync(Identifier identifier)
        {
            identifier.UserId = _userId;
            while (await _backend.GetOrCreateUserIdAsync(identifier) is var response && Application.isPlaying)
            {
                if (response.UserId == -1)
                {
                    await Task.Delay(GET_ID_RETRY_INTERVAL);
                    Log.Info("retry");
                }
                else
                {
                    _userId = response.UserId;
                    PlayerPrefs.SetInt(USER_ID_PREF, Convert.ToInt32(_userId));
                    return response.IsUserNew;
                }
            }
            Log.Info("Success. Start sending task");
            return false;
        }

        public long GetUserId()
        {
            return _userId;
        }
    }
}