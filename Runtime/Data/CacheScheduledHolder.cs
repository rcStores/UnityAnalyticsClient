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
		private CancellationTokenSource _sendingCancellationSource; 
		
		private Session _session;

        private readonly Backend 				_backend;		
		private readonly GamePropertiesPool 	_properties;
        private readonly GameEventsPool 		_events;
		
		private readonly string _propsPath;
        private readonly string _eventsPath;
        private readonly string _usersTable;		
			
		private const string USER_ID_PREF 				= "UserId";
        private const string APP_VERSION_PREF 			= "AppVersion";
        private const string CACHED_EVENTS_FILE 		= "Events.dat";
        private const string CACHED_PROPERTIES_FILE 	= "Properties.dat";
		
		private const int SENDING_INTERVAL 				= 120000; // 2 min in ms
		private const int MAX_CACHE_COUNT 				= 10; 
		private const int GAME_EVENT_PARAMETER_COUNT 	= 10;

        public CacheScheduledHolder(string usersTableName, Backend backend)
        {
			string serializationPath = null;
// ------------------------------------------------------------------------------------------------------
#if UNITY_IOS || UNITY_EDITOR
            serializationPath = Path.Combine(Application.persistentDataPath, "CachedData");
// ------------------------------------------------------------------------------------------------------
#else
            serializationPath = Path.Combine(AndroidUtils.ApiUtil.GetPersistentDataPath(), "CachedData");
#endif
// ------------------------------------------------------------------------------------------------------

            _backend = backend;
			
            if (!Directory.Exists(serializationPath))
            {
                Directory.CreateDirectory(serializationPath);
            }
            _eventsPath 	= Path.Combine(serializationPath, CACHED_EVENTS_FILE);
            _propsPath 		= Path.Combine(serializationPath, CACHED_PROPERTIES_FILE);

            _events 		= Deserialize<GameEventsPool>(_eventsPath);
            _properties 	= Deserialize<GamePropertiesPool>(_propsPath);

            _usersTable = usersTableName;
        }
		
		public ref GameEvent NewEvent(string eventName)
		{
			ref GameEvent e = ref _events.NewElement();
			
			e.Free();
			e.SetTimestamp(DateTime.UtcNow);
			e.SetMaxParameterCount(GAME_EVENT_PARAMETER_COUNT);
			e.SetName(eventName);
			
			if (_events.GetCurrentBusyCount() >= MAX_CACHE_COUNT)
			{
				//Debug.LogWarning("[ADVANAL] STOP DELAYING THE SENDING OPERATION");
				_sendingCancellationSource?.Cancel();
			}
			//Debug.LogWarning("[ADVANAL] RETURNING EVENT REFERENCE");
			return ref e;
		}
		
		public void NewProperty(string name, int value, string tableName)
		{
			ref var p = ref _properties.NewElement();
			p.Set(name, value);
			p.SetTableName(tableName);
		}
		
		public void NewProperty(string name, double value, string tableName)
		{
			ref var p = ref _properties.NewElement();
			p.Set(name, value);
			p.SetTableName(tableName);
		}
		
		public void NewProperty(string name, string value, string tableName)
		{
			ref var p = ref _properties.NewElement();
			p.Set(name, value);
			p.SetTableName(tableName);
		}
		
		public void NewProperty(string name, bool value, string tableName)
		{
			ref var p = ref _properties.NewElement();
			p.Set(name, value);
			p.SetTableName(tableName);
		}
		
		public void NewProperty(string name, DateTime value, string tableName)
		{
			ref var p = ref _properties.NewElement();
			p.Set(name, value);
			p.SetTableName(tableName);
		}
		
		public void NewSession(long sessionCount, int gameArea)
		{
			_session.SetSessionCount(sessionCount);
			_session.SetArea(gameArea);
		}
		
		public void BewSession(int gameArea)
		{
			_session.SetArea(gameArea);
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

        public async UniTask StartSendingDataAsync(long id)
        {
			Log.Info("Start scheduler");
            Debug.Assert(id != -1);

            await RunSendingLoop(id);
        }
		
		private void SerializeEvents()
        {
            Serialize<GameEventsPool>(_eventsPath, _events);
        }

        private void SerializeProperties()
        {
            Serialize<GamePropertiesPool>(_propsPath, _properties);
        }

        public void Serialize<T>(string filePath, T data)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            try
            {
				using var fs = new FileStream(filePath, FileMode.OpenOrCreate);
                formatter.Serialize(fs, data);
            }
            catch (Exception e)
            {
                Log.Info("Failed to serialize. Reason: " + e.Message);
            }
        }

        public TPool Deserialize<TPool>(string filePath) where TPool : new()
        {
			TPool result = default(TPool);
			
            if (!File.Exists(filePath))
            {
                result = new TPool();
            }
			else 
			{
				try
				{
					using var fs 	= new FileStream(filePath, FileMode.Open);
					var formatter 	= new BinaryFormatter();
				
					result = (TPool)formatter.Deserialize(fs);
				}
				catch (Exception)
				{
					result = new TPool();
				}
				finally
				{
					File.Delete(filePath);
				}
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
				
				await UniTask.Delay(TimeSpan.FromMinutes(2), false, PlayerLoopTiming.PostLateUpdate,_sendingCancellationSource.Token)
					.SuppressCancellationThrow();
						
				_sendingCancellationSource = null;
					
				//Debug.LogWarning("[ADVANAL] SENDING ANALYTICS DATA");

				int eventsBatchSize 		= _events.GetCurrentBusyCount();
				int propertiesBatchSize 	= _properties.GetCurrentBusyCount();
				
				var lastActivity = _session.GetLastActivty();
				if (lastActivity != default(DateTime) && DateTime.UtcNow.Subtract(lastActivity) > TimeSpan.FromMinutes(10))
				{
					long sessionCount = _session.GetSessionCount() + 1;
					_session.SetSessionCount(sessionCount);
					await backend.PutSessionCount(userId, sessionCount);
				}
				
				var eventsSending 		= _backend.SendToServerAsync<GameEvent>(await _events.ToJsonAsync(userId));					
				var propertiesSending 	= _backend.SendToServerAsync<GameProperty>(await _properties.ToJsonAsync(userId));
				var sessionSending		= _backend.SendToServerAsync<Session>(_session.ToJson(userId));
						
				var (hasEventsSendingSucceeded, hasPropertiesSendingSucceeded, hasSessionSendingSucceeded) = 
					await UniTask.WhenAll(eventsSending, propertiesSending, sessionSending);
				
				//Debug.LogWarning("[ADVANAL] Getting results of data sending...");
				
				if (hasEventsSendingSucceeded) 
				{
					//Debug.LogWarning("[ADVANAL] Clear events");
					_events.FreeFromBeginning(eventsBatchSize);
				}
				if (hasPropertiesSendingSucceeded)
				{
					//Debug.LogWarning("[ADVANAL] Clear properties");
					_properties.FreeFromBeginning(propertiesBatchSize);
				}
				// if (hasSessionSendingSucceeded)
				// {
					// _session.MarkAsRegistered();
				// }
			}
        }
		
				// var continuationTask = Task.Delay(SENDING_INTERVAL, _sendingCancellationSource.Token)
					// .ContinueWith(task => { });
				// await continuationTask;
		
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
