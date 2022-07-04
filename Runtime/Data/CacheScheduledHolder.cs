using Advant.Http;
using Advant.Data.Models;
using Advant.Logging;

using System.Threading.Tasks;
using UnityEngine;
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;

namespace Advant.Data
{
    [System.ComponentModel.DesignerCategory("Code")]
    internal class CacheScheduledHolder
    {
        private const int SENDING_INTERVAL = 120000; // 2 min in ms
        private const int GET_ID_RETRY_INTERVAL = 15000; 

		private const string USER_ID_PREF = "UserId";
        private long _userId;

        private readonly Backend _backend;

        private readonly Cache<GameProperty> _gameProperties;
        private readonly Cache<GameEvent> _gameEvents;
        
        private bool _arePropertiesProcessing = false;
        private bool _areEventsProcessing = false;

        private const string CACHED_EVENTS_FILE = "Events.dat";
        private const string CACHED_PROPERTIES_FILE = "Properties.dat";

        private readonly string _propsPath;
        private readonly string _eventsPath;


        public CacheScheduledHolder(Backend backend)
        {
			string serializationPath = null;
#if UNITY_ANDROID && !UNITY_EDITOR
            serializationPath = Path.Combine(RosUtils.AndroidApiUtil.GetPersistentDataPath(), "CachedData");
#elif UNITY_IOS || UNITY_EDITOR
            serializationPath = Path.Combine(Application.persistentDataPath, "CachedData");
#endif
            _backend = backend;
			_userId = Convert.ToInt64(PlayerPrefs.GetInt(USER_ID_PREF, -1));
            if (!Directory.Exists(serializationPath))
            {
                Directory.CreateDirectory(serializationPath);
            }
            _eventsPath = Path.Combine(serializationPath, CACHED_EVENTS_FILE);
            _propsPath = Path.Combine(serializationPath, CACHED_PROPERTIES_FILE);

            _gameEvents = Deserialize<GameEvent>(_eventsPath);
            _gameProperties = Deserialize<GameProperty>(_propsPath);
        }

        public async void Put(GameProperty gameProperty)
        {
            while (_arePropertiesProcessing)
            {
                await Task.Yield();
            }
            _gameProperties.AddUnique(gameProperty);
        }

        public async void Put(GameEvent gameEvent)
        {
            while (_areEventsProcessing)
            {
                await Task.Yield();
            }
            _gameEvents.Add(gameEvent);
        }

        public void SaveCacheLocally()
        {
            SerializeEvents();
            SerializeProperties();
        }

        public async Task StartAsync(Identifier identifier)
        {
            Log.Info("Start scheduler. Getting user id...");

            while (await _backend.GetOrCreateUserIdAsync(identifier) is var userId && Application.isPlaying)
            {
                if (userId == -1)
                {
                    await Task.Delay(GET_ID_RETRY_INTERVAL);
                    Log.Info("retry");
                }
                else
                {
                    _userId = userId;
					PlayerPrefs.SetInt(USER_ID_PREF, Convert.ToInt32(_userId));
                    break;
                }
            }
            Log.Info("Success. Start sending task");
            RunSendingLoop(_userId);
        }

        private void SerializeEvents()
        {
            Serialize(_eventsPath, _gameEvents);
        }

        private void SerializeProperties()
        {
            File.Delete(_propsPath);
        }

        public void Serialize<T>(string filePath, Cache<T> data) where T : IGameData 
        {
            var fs = new FileStream(filePath, FileMode.OpenOrCreate);

            BinaryFormatter formatter = new BinaryFormatter();
            try
            {
                formatter.Serialize(fs, data);
            }
            catch (SerializationException e)
            {
                Log.Info("Failed to serialize. Reason: " + e.Message);
            }
            finally
            {
                fs.Close();
            }
        }

        public Cache<T> Deserialize<T>(string filePath) where T : IGameData
        {
            if (!File.Exists(filePath))
            {
                return new Cache<T>();
            }

            var fs = new FileStream(filePath, FileMode.Open);
            Cache<T> result = null;
            try
            {
                BinaryFormatter formatter = new BinaryFormatter();
                result = (Cache<T>)formatter.Deserialize(fs);
            }
            catch (SerializationException)
            {
                return new Cache<T>();
            }
            finally
            {
                fs.Close();
                File.Delete(filePath);
            }

            return result;
        }

        private async void RunSendingLoop(long userId)
        {
            while (Application.isPlaying)
            {
                await Task.Delay(SENDING_INTERVAL);

                bool hasPropertiesSendingSucceeded = true;
                bool hasEventsSendingSucceeded = true;

                _arePropertiesProcessing = true;
                _areEventsProcessing = true;

                Task propertiesSending = null, eventsSending = null;
                try
                {
                    eventsSending =  _backend.SendToServerAsync(userId, _gameEvents);
                    propertiesSending = _backend.SendToServerAsync(userId, _gameProperties);
                    await Task.WhenAll(propertiesSending, eventsSending);
                }
                catch (Exception)
                {
                    hasPropertiesSendingSucceeded = !propertiesSending.IsFaulted;
                    hasEventsSendingSucceeded = !eventsSending.IsFaulted;
                }
                
                if (hasPropertiesSendingSucceeded)
                {
                    Log.Info("Clear properties");
                    _gameProperties.Clear();
                }

                if (hasEventsSendingSucceeded)
                {
                    Log.Info("Clear events");
                    _gameEvents.Clear();
                }

                _arePropertiesProcessing = false;
                _areEventsProcessing = false;
            }
        }
    }
}
