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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Advant.Data
{
    internal class CacheScheduledHolder
    {
        private const int SENDING_INTERVAL = 120000; // 2 min in ms
		private CancellationTokenSource _sendingCancellationSource; 

		private const string USER_ID_PREF = "UserId";
        private const string APP_VERSION_PREF = "AppVersion";

        private readonly Backend _backend;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1);
        private readonly ConcurrentQueue<GameProperty> _gameProperties;
        private readonly ConcurrentQueue<GameEvent> _gameEvents;
        private const int MAX_CACHE_COUNT = 10;
        private int _currentEventsCount = 0;

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
        }

        public async void Put(GameProperty gameProperty)
        {
            _gameProperties.Enqueue(gameProperty);
        }

        public async void Put(GameEvent gameEvent)
        {
            _gameEvents.Enqueue(gameEvent);
            Interlocked.Increment(ref _currentEventsCount);
			
			if (_currentEventsCount >= MAX_CACHE_COUNT && !_areEventsProcessing) 
			{
				_areEventsProcessing = true;
				Volatile.Write(ref _areEventsProcessing, true);
				Debug.LogWarning("[ADVANAL] STOP DELAYING THE SENDING OPERATION");
				_sendingCancellationSource?.Cancel();
			} 
        }

        public void SaveCacheLocally()
        {
			try
			{
				//Debug.LogWarning("[ADVANAL] Saving cache locally");
				SerializeEvents();
				SerializeProperties();
			}
			catch (Exception e)
			{
				Debug.LogWarning("Saving cache failure: " + e.Message);
			}
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

        public void Serialize<T>(string filePath, ConcurrentQueue<T> data) where T : IGameData 
        {
            var fs = new FileStream(filePath, FileMode.OpenOrCreate);

            BinaryFormatter formatter = new BinaryFormatter();
            try
            {
                formatter.Serialize(fs, data.ToArray());
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

        public ConcurrentQueue<T> Deserialize<T>(string filePath) where T : IGameData
        {
            if (!File.Exists(filePath))
            {
                return new ConcurrentQueue<T>();
            }

            var fs = new FileStream(filePath, FileMode.Open);
            ConcurrentQueue<T> result = null;
            try
            {
                BinaryFormatter formatter = new BinaryFormatter();
                result = new ConcurrentQueue<T>((IEnumerable<T>)formatter.Deserialize(fs));
            }
			catch (InvalidCastException e)
			{
				var deserializedCache = (Cache<T>)formatter.Deserialize(fs);
				result = new ConcurrentQueue<T>(deserializedCache.Get());
			}
            catch (SerializationException)
            {
                result = new ConcurrentQueue<T>();
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
			while (true)		
            {
#if UNITY_EDITOR
				if (!Application.isPlaying) return;
#endif				
				_sendingCancellationSource = new CancellationTokenSource();
				var continuationTask = Task.Delay(SENDING_INTERVAL, _sendingCancellationSource.Token)
					.ContinueWith(task => { });
				await continuationTask;
					
				Debug.LogWarning("[ADVANAL] SENDING ANALYTICS DATA");

                bool hasPropertiesSendingSucceeded = true;
                bool hasEventsSendingSucceeded = true;

                // _arePropertiesProcessing = true;
                 //_areEventsProcessing = true;
				Volatile.Write(ref _areEventsProcessing, true);
				
                 var gameEvents = new Cache<GameEvent>(_gameEvents.ToArray());
                 var gameProperties = new Cache<GameProperty>(_gameProperties.ToArray());
				
				// Debug.LogWarning("[ADVANAL] BUFFER SNAPSHOT\nEVENTS:\n");
				// foreach (var e in gameEvents.Get())
				// {
					// Debug.LogWarning(e.Name);
					// if (e._parameters != null)
					// {
						// foreach (var param in e._parameters)
						// {
							// Debug.Log(param.Key + "=" + param.Value);
						// }
					// }
				// }

                Task propertiesSending = null, eventsSending = null;
                try
                {
                    if (gameEvents == null || gameProperties == null)
                    {
                        Debug.LogWarning("Cache instance(s) == null");
                    }

					if (gameEvents.Count == 0)
					{
						Debug.LogWarning("Events buffer is empty");
					} 
					else
					{
						eventsSending =  _backend.SendToServerAsync(userId, gameEvents);
					}
					
					if (gameProperties.Count == 0)
					{
						Debug.LogWarning("Properties buffer is empty");
					} 
					else
					{
						propertiesSending =  _backend.SendToServerAsync(userId, gameProperties);
					}

                    await Task.WhenAll(new Task[] { eventsSending, propertiesSending }.Where(i => i != null));
                }
                catch (Exception e)
                {
					Debug.LogWarning("[ADVANAL] Error while sending data: " + e.Message);
                    Debug.LogWarning("Stack trace: " + e.StackTrace);
                    Debug.LogWarning("Source: " + e.Source);
                }
                finally
				{
					Debug.LogWarning("[ADVANAL] Getting results of data sending...");
					hasPropertiesSendingSucceeded = propertiesSending == null ? 
						false : !propertiesSending.IsFaulted;
                    hasEventsSendingSucceeded = eventsSending == null ?
						false : !eventsSending.IsFaulted;
				}
                
                if (hasPropertiesSendingSucceeded)
                {
                    Debug.LogWarning("[ADVANAL] Clear properties");
					//_gameProperties.Clear();
                    foreach (var _ in gameProperties.Get())
                    {
						if (!_gameProperties.TryDequeue(out var _))
                        {
							Debug.LogWarning("[ADVANAL] GameProperty isn't taken from the queue");
                        }
					}
                }

                if (hasEventsSendingSucceeded)
                {
                    Debug.LogWarning("[ADVANAL] Clear events");
					//_gameEvents.Clear();
                    foreach (var _ in gameEvents.Get())
                    {
						if (!_gameEvents.TryDequeue(out var _))
                        {
							Debug.LogWarning("[ADVANAL] GameEvent isn't taken from the queue");
                        }
						else Interlocked.Decrement(ref _currentEventsCount);
                    }
                }
				Volatile.Write(ref _areEventsProcessing, false);
            }
        }
    }
}
