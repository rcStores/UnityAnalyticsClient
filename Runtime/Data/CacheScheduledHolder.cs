using Advant.Http;
using Advant.Data.Models;
using Advant.Logging;

using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;


namespace Advant.Data
{
    internal class CacheScheduledHolder
    {
        private const int SENDING_INTERVAL = 90000; // 1.5 min in ms
		private readonly CancellationTokenSource _sendingCancellationSource; 

		private const string USER_ID_PREF = "UserId";
        private const string APP_VERSION_PREF = "AppVersion";
        private const int MAX_CACHE_COUNT = 10;

        private readonly Backend _backend;

        private readonly Cache<GameProperty> _gameProperties;
        private readonly Cache<GameEvent> _gameEvents;
        
        private bool _arePropertiesProcessing = false;
        private bool _areEventsProcessing = false;

        private const string CACHED_EVENTS_FILE = "Events.dat";
        private const string CACHED_PROPERTIES_FILE = "Properties.dat";

        private readonly string _propsPath;
        private readonly string _eventsPath;

        private readonly string _usersTable;

        public CacheScheduledHolder(string usersTableName, Backend backend)
        {
			string serializationPath = null;
#if UNITY_IOS || UNITY_EDITOR
            serializationPath = Path.Combine(Application.persistentDataPath, "CachedData");
#else
            serializationPath = Path.Combine(AndroidUtils.ApiUtil.GetPersistentDataPath(), "CachedData");
#endif
            _backend = backend;
            if (!Directory.Exists(serializationPath))
            {
                Directory.CreateDirectory(serializationPath);
            }
            _eventsPath = Path.Combine(serializationPath, CACHED_EVENTS_FILE);
            _propsPath = Path.Combine(serializationPath, CACHED_PROPERTIES_FILE);

            _gameEvents = Deserialize<GameEvent>(_eventsPath);
            _gameProperties = Deserialize<GameProperty>(_propsPath);

            _usersTable = usersTableName;
			
			_sendingCancellationSource = new CancellationTokenSource();
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
			if (_gameEvents.Count >= MAX_CACHE_COUNT) _sendingCancellationSource.Cancel();
        }

        public void SaveCacheLocally()
        {
            SerializeEvents();
            SerializeProperties();
        }

        public async Task StartSendingDataAsync(long id)
        {
			Log.Info("Start scheduler");
            Debug.Assert(id != -1);

            await RunSendingLoop(id);
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

        private async Task RunSendingLoop(long userId)
        {
			
            while (Application.isPlaying)
            {
                await Task.Delay(SENDING_INTERVAL, _sendingCancellationSource.Token);
					
				Debug.LogError("[ADVANAL] SENDING ANALYTICS DATA");

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
