using Advant.Http;
using Advant.Data.Models;

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

        private long _userId = -1;

        private readonly Backend _backend;

        private readonly Cache<GameProperty> _gameProperties;
        private readonly Cache<GameEvent> _gameEvents;
        
        private bool _arePropertiesProcessing = false;
        private bool _areEventsProcessing = false;

        private readonly string SERIALIZATION_PATH = Path.Combine(Application.dataPath, "CachedData");
        private readonly string CACHED_EVENTS_FILE = "Events.dat";
        private readonly string CACHED_PROPERTIES_FILE = "Properties.dat";

        private readonly string _propsPath;
        private readonly string _eventsPath;


        public CacheScheduledHolder(Backend backend)
        {
            _backend = backend;

            if (!Directory.Exists(SERIALIZATION_PATH))
            {
                Directory.CreateDirectory(SERIALIZATION_PATH);
            }
            _eventsPath = Path.Combine(SERIALIZATION_PATH, CACHED_EVENTS_FILE);
            _propsPath = Path.Combine(SERIALIZATION_PATH, CACHED_PROPERTIES_FILE);

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
            Debug.Log("Start scheduler. Getting user id...");

            while (await _backend.GetOrCreateUserIdAsync(identifier) is var userId)
            {
                if (userId == -1)
                {
                    await Task.Delay(GET_ID_RETRY_INTERVAL);
                    Debug.Log("retry");
                }
                else
                {
                    _userId = userId;
                    break;
                }
            }
            Debug.Log("Success. Start sending task");
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
                Debug.Log("Failed to serialize. Reason: " + e.Message);
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
            while (true)
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
                    Debug.Log("Clear properties");
                    _gameProperties.Clear();
                }

                if (hasEventsSendingSucceeded)
                {
                    Debug.Log("Clear events");
                    _gameEvents.Clear();
                }

                _arePropertiesProcessing = false;
                _areEventsProcessing = false;
            }
        }
    }
}
