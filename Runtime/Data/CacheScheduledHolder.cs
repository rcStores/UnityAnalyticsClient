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
using Cysharp.Threading.Tasks;

namespace Advant.Data
{
    internal class CacheScheduledHolder
    {
        private const int SENDING_INTERVAL = 120000; // 2 min in ms
		private CancellationTokenSource _sendingCancellationSource; 

		private const string USER_ID_PREF = "UserId";
        private const string APP_VERSION_PREF = "AppVersion";

        private readonly Backend _backend;

        // private readonly ConcurrentQueue<GameProperty> _gameProperties;
        // private readonly ConcurrentQueue<GameEvent> _gameEvents;
		
		private readonly SimplePool<GameProperty> _properties;
        private readonly SimplePool<GameEvent> _events;
		
        private const int MAX_CACHE_COUNT = 10;
        private int _currentEventsCount = 0;
		
		private bool _isSendingRunning = false;
		
		// private GameDataPool<GameEvent> _events = new GameDataPool(MAX_EVENTS_SIZE);
		// private GameDataPool<GameProperty> _properties = new GameDataPool(MAX_PROPERTIES_SIZE);

        // private bool _arePropertiesProcessing = false;
        // private bool _areEventsProcessing = false;

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

            _events = Deserialize<GameEvent>(_eventsPath);
            _properties = Deserialize<GameProperty>(_propsPath);

            _usersTable = usersTableName;
        }
		
		public ref GameEvent NewEvent()
		{
			ref GameEvent e = ref _events.NewElement();
			e.SetTimestamp(DateTime.UtcNow);
			e.SetMaxParameterCount(10);
			if (_events.GetCurrentBusyCount() >= MAX_CACHE_COUNT)
			{
				Debug.LogWarning("[ADVANAL] STOP DELAYING THE SENDING OPERATION");
				_sendingCancellationSource?.Cancel();
			}
			Debug.LogWarning("[ADVANAL] RETURNING EVENT REFERENCE");
			return ref e;
		}
		
		// public void SendEvent(int idx)
		// {
			// _events.MarkAsBusy(idx);
			// if (_events.GetCurrentBusyCount() >= MAX_CACHE_COUNT)
			// {
				// Debug.LogWarning("[ADVANAL] STOP DELAYING THE SENDING OPERATION");
				// _sendingCancellationSource?.Cancel();
			// }			
		// }
		
		public ref GameProperty NewProperty()
		{
			return ref _properties.NewElement();
		}
		
		// public void SendProperty(int idx)
		// {
			// _properties.MarkAsBusy(idx);
		// }

        // public async void Put(GameProperty gameProperty)
        // {
            // _gameProperties.Enqueue(gameProperty);
        // }

        // public async void Put(GameEvent gameEvent)
        // {
            // _gameEvents.Enqueue(gameEvent);
            // Interlocked.Increment(ref _currentEventsCount);
			
			// if (_currentEventsCount >= MAX_CACHE_COUNT && !_areEventsProcessing) 
			// {
				// _areEventsProcessing = true;
				// Volatile.Write(ref _areEventsProcessing, true);
				// Debug.LogWarning("[ADVANAL] STOP DELAYING THE SENDING OPERATION");
				// _sendingCancellationSource?.Cancel();
			// } 
        // }

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

        public async UniTask StartSendingDataAsync(long id)
        {
			Log.Info("Start scheduler");
            Debug.Assert(id != -1);

            await RunSendingLoop(id);
        }
		
		private void SerializeEvents()
        {
            Serialize(_eventsPath, _events);
        }

        private void SerializeProperties()
        {
            Serialize(_propsPath, _properties);
        }

        public void Serialize<T>(string filePath, SimplePool<T> data) where T : IGameData 
        {
            FileStream fs = null;

            BinaryFormatter formatter = new BinaryFormatter();
            try
            {
				fs = new FileStream(filePath, FileMode.OpenOrCreate);
                formatter.Serialize(fs, data);
            }
            catch (Exception e)
            {
                Log.Info("Failed to serialize. Reason: " + e.Message);
            }
            finally
            {
                fs.Close();
            }
        }

        public SimplePool<T> Deserialize<T>(string filePath) where T : IGameData
        {
            if (!File.Exists(filePath))
            {
                return new SimplePool<T>(10);
            }

            FileStream fs = null;
            SimplePool<T> result = null;
			BinaryFormatter formatter = null;
            try
            {
				fs = new FileStream(filePath, FileMode.Open);
                formatter = new BinaryFormatter();
                //result = new ConcurrentQueue<T>((IEnumerable<T>)formatter.Deserialize(fs));
				result = (SimplePool<T>)formatter.Deserialize(fs);
            }
			catch (Exception)
            {
                result = new SimplePool<T>(10);
            }
            finally
            {
                fs.Close();
                File.Delete(filePath);
            }
			return result;
        }

        private async UniTask RunSendingLoop(long userId)
        {
			while (true)		
            {
#if UNITY_EDITOR
				if (!Application.isPlaying) return;
#endif				
				_sendingCancellationSource = new CancellationTokenSource();
				// var continuationTask = Task.Delay(SENDING_INTERVAL, _sendingCancellationSource.Token)
					// .ContinueWith(task => { });
				// await continuationTask;
				await UniTask.Delay(
					TimeSpan.FromMinutes(2), 
					false, 
					PlayerLoopTiming.PostLateUpdate, 
					_sendingCancellationSource.Token)
						.SuppressCancellationThrow();
						
				_sendingCancellationSource = null;
					
				Debug.LogWarning("[ADVANAL] SENDING ANALYTICS DATA");

                bool hasPropertiesSendingSucceeded = true;
				bool hasEventsSendingSucceeded = true;
				int eventsBatchSize = 0;
				int propertiesBatchSize = 0;
				UniTask propertiesSending = null, eventsSending = null;
				try
				{
					eventsBatchSize = _events.GetCurrentBusyCount();
					propertiesBatchSize = _properties.GetCurrentBusyCount();
					
					eventsSending = eventsBatchSize > 0 ?
						_backend.SendToServerAsync<GameEvent>(await _events.ToJson(userId)) :
						UniTask.CompletedTask;
					propertiesSending = propertiesBatchSize > 0 ?
						_backend.SendToServerAsync<GameProperty>(await _properties.ToJson(userId)) :
						UniTask.CompletedTask;
						
					await UniTask.WhenAll(eventsSending, propertiesSending);
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
				
				if (hasEventsSendingSucceeded) 
				{
					_events.MarkAsSended(eventsBatchSize);
				}
				if (hasPropertiesSendingSucceeded)
				{
					_properties.MarkAsSended(propertiesBatchSize);
				}
			}
				// }
        }
		
				// bool hasPropertiesSendingSucceeded = true;
                // bool hasEventsSendingSucceeded = true;

                // //_arePropertiesProcessing = true;
                // //_areEventsProcessing = true;
				// Volatile.Write(ref _areEventsProcessing, true);
				
                // var gameEvents = new Cache<GameEvent>(_gameEvents.ToArray());
                // var gameProperties = new Cache<GameProperty>(_gameProperties.ToArray());
				
				// // Debug.LogWarning("[ADVANAL] BUFFER SNAPSHOT\nEVENTS:\n");
				// // foreach (var e in gameEvents.Get())
				// // {
					// // Debug.LogWarning(e.Name);
					// // if (e._parameters != null)
					// // {
						// // foreach (var param in e._parameters)
						// // {
							// // Debug.Log(param.Key + "=" + param.Value);
						// // }
					// // }
				// // }

                // Task propertiesSending = null, eventsSending = null;
                // try
                // {
                    // if (gameEvents == null || gameProperties == null)
                    // {
                        // Debug.LogWarning("Cache instance(s) == null");
                    // }

					// if (gameEvents.Count == 0)
					// {
						// Debug.LogWarning("Events buffer is empty");
					// } 
					// else
					// {
						// eventsSending =  _backend.SendToServerAsync(userId, gameEvents);
					// }
					
					// if (gameProperties.Count == 0)
					// {
						// Debug.LogWarning("Properties buffer is empty");
					// } 
					// else
					// {
						// propertiesSending =  _backend.SendToServerAsync(userId, gameProperties);
					// }

                    // await Task.WhenAll(new Task[] { eventsSending, propertiesSending }.Where(i => i != null));
                // }
                // catch (Exception e)
                // {
					// Debug.LogWarning("[ADVANAL] Error while sending data: " + e.Message);
                    // Debug.LogWarning("Stack trace: " + e.StackTrace);
                    // Debug.LogWarning("Source: " + e.Source);
                // }
                // finally
				// {
					// Debug.LogWarning("[ADVANAL] Getting results of data sending...");
					// hasPropertiesSendingSucceeded = propertiesSending == null ? 
						// false : !propertiesSending.IsFaulted;
                    // hasEventsSendingSucceeded = eventsSending == null ?
						// false : !eventsSending.IsFaulted;
				// }
                
                // if (hasPropertiesSendingSucceeded)
                // {
                    // Debug.LogWarning("[ADVANAL] Clear properties");
					// //_gameProperties.Clear();
                    // foreach (var _ in gameProperties.Get())
                    // {
						// if (!_gameProperties.TryDequeue(out var _))
                        // {
							// Debug.LogWarning("[ADVANAL] GameProperty isn't taken from the queue");
                        // }
					// }
                // }

                // if (hasEventsSendingSucceeded)
                // {
                    // Debug.LogWarning("[ADVANAL] Clear events");
					// //_gameEvents.Clear();
                    // foreach (var _ in gameEvents.Get())
                    // {
						// if (!_gameEvents.TryDequeue(out var _))
                        // {
							// Debug.LogWarning("[ADVANAL] GameEvent isn't taken from the queue");
                        // }
						// else Interlocked.Decrement(ref _currentEventsCount);
                    // }
                // }
				// Volatile.Write(ref _areEventsProcessing, false);
            // }	
    }
}
