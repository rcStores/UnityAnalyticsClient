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
		private CancellationTokenSource 	_sendingCancellationSource; 
		private long 						_userId = -1;
		
		private HashSet<int> 				_excludedGlobals		= new HashSet<int>();
		private List<Value> 				_globalEventParams		= new List<Value>();
		private Dictionary<string, int>		_indicesOfGlobalsByName	= new Dictionary<string, int>();

        private readonly Backend 				_backend;		
		private readonly GamePropertiesPool 	_properties;
        private readonly GameEventsPool 		_events;
		private readonly GameSessionsPool 		_sessions;
		
		private readonly string _propsPath;
        private readonly string _eventsPath;
		private readonly string _sessionsPath;
        private readonly string _usersTable;		
			
		private const string USER_ID_PREF 				= "UserId";
        private const string APP_VERSION_PREF 			= "AppVersion";
        private const string CACHED_EVENTS_FILE 		= "Events.dat";
        private const string CACHED_PROPERTIES_FILE 	= "Properties.dat";
		private const string CACHED_SESSIONS_FILE 		= "Sessions.dat";
		
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
			_sessionsPath 	= Path.Combine(serializationPath, CACHED_SESSIONS_FILE);

            _events 		= Deserialize<GameEventsPool>(_eventsPath);
            _properties 	= Deserialize<GamePropertiesPool>(_propsPath);
			_sessions	 	= Deserialize<GameSessionsPool>(_sessionsPath);

            _usersTable = usersTableName;
			
			_globalEventParams.Capacity = GAME_EVENT_PARAMETER_COUNT;
        }
		
		public ref GameEvent NewEvent(string eventName, params string[] globalsLookupSource)
		{
			if (globalsLookupSource != null)
			{
				foreach (var paramName in globalsLookupSource)
				{
					if (paramName == null) break;
					
					string formattedName = paramName.Replace('-', '_');
					if (_indicesOfGlobalsByName.TryGetValue(formattedName, out int idx))
						_excludedGlobals.Add(idx);
				}
			}
			return ref NewEventImpl(eventName);
		}
		
		public ref GameEvent NewEvent(string eventName) => ref NewEventImpl(eventName);
		
		private ref GameEvent NewEventImpl(string eventName)
		{
			ref GameEvent e = ref _events.NewElement();
			
			e.Free();
			e.SetTimestamp(DateTime.UtcNow);
			e.SetMaxParameterCount(GAME_EVENT_PARAMETER_COUNT);
			e.SetName(eventName);
			
			for (int i = 0; i < _globalEventParams.Count; ++i)
			{
				if (!_excludedGlobals.Contains(i))
				{
					e.Add(_globalEventParams[i].Name, 
						  _globalEventParams[i].Data, 
						  _globalEventParams[i].Type);
				}
			}
			_excludedGlobals.Clear();
			
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
		
		public void SetSessionCount(long sessionCount)	=> _sessions.SetUserSessionCount(sessionCount);
		public void SetSessionStart() 					=> _sessions.SetSessionStart();
		
		// public void NewSession(long sessionCount, int gameArea, string abMode)
		// {
			// ref var s 		= ref _sessions.NewElement();
			// s.SessionStart	= DateTime.UtcNow;
			// s.LastActivity	= DateTime.UtcNow;
			// s.SessionCount 	= sessionCount;
			// s.Area 			= gameArea;
			// s.AbMode 		= abMode;
		// }
		
		public void SetCurrentArea(int area)
		{
			_sessions.SetCurrentArea(area);
			SetGlobalEventParam("area", area);
		}
		
		public void SetCurrentAbMode(string abMode, string propertyTableName)
		{
			_sessions.SetCurrentAbMode(abMode);	
			SetGlobalEventParam("ab_mode", abMode);
			NewProperty("ab_mode", abMode, propertyTableName);
		}
		
		public void SetGlobalEventParam(string name, int value)
		{
			string formattedName = name.Replace('-', '_');
			if (!_indicesOfGlobalsByName.TryGetValue(formattedName, out int idx))
			{
				var param = new Value();
				param.Set(formattedName, value);
				_globalEventParams.Add(param);
				_indicesOfGlobalsByName[formattedName] = _globalEventParams.Count - 1;
			}
			else
			{
				_globalEventParams[idx].Set(formattedName, value);
			}
		}
		
		public void SetGlobalEventParam(string name, double value)
		{
			string formattedName = name.Replace('-', '_');
			if (!_indicesOfGlobalsByName.TryGetValue(formattedName, out int idx))
			{
				var param = new Value();
				param.Set(formattedName, value);
				_globalEventParams.Add(param);
				_indicesOfGlobalsByName[formattedName] = _globalEventParams.Count - 1;
			}
			else
			{
				_globalEventParams[idx].Set(formattedName, value);
			}
		}
		
		public void SetGlobalEventParam(string name, bool value)
		{
			string formattedName = name.Replace('-', '_');
			if (!_indicesOfGlobalsByName.TryGetValue(formattedName, out int idx))
			{
				var param = new Value();
				param.Set(formattedName, value);
				_globalEventParams.Add(param);
				_indicesOfGlobalsByName[formattedName] = _globalEventParams.Count - 1;
			}
			else
			{
				_globalEventParams[idx].Set(formattedName, value);
			}
		}
		
		public void SetGlobalEventParam(string name, string value)
		{
			string formattedName = name.Replace('-', '_');
			if (!_indicesOfGlobalsByName.TryGetValue(formattedName, out int idx))
			{
				var param = new Value();
				param.Set(formattedName, value);
				_globalEventParams.Add(param);
				_indicesOfGlobalsByName[formattedName] = _globalEventParams.Count - 1;
			}
			else
			{
				_globalEventParams[idx].Set(formattedName, value);
			}
		}
		
		public void SetGlobalEventParam(string name, DateTime value)
		{
			string formattedName = name.Replace('-', '_');
			if (!_indicesOfGlobalsByName.TryGetValue(formattedName, out int idx))
			{
				var param = new Value();
				param.Set(formattedName, value);
				_globalEventParams.Add(param);
				_indicesOfGlobalsByName[formattedName] = _globalEventParams.Count - 1;
			}
			else
			{
				_globalEventParams[idx].Set(formattedName, value);
			}
		}
		
		public async UniTask<bool> RegisterActivity()
		{
			//var start = DateTime.UtcNow;
			bool isSessionNew = false;
			try 
			{
				if (TryUpdateSessionCount() is var isInnerSessionUpdated && isInnerSessionUpdated &&
					await _backend.PutSessionCount(_userId, _sessions.GetSession().SessionCount))
				{
					isSessionNew = true;
				}
				else if (isInnerSessionUpdated)
				{
					_sessions.ClearLastSession();
					return false;
				}
				_sessions.GetSession().LastActivity = DateTime.UtcNow;
			}
			catch (Exception e)
			{
				Debug.LogError("[ADVANT] RegisterActivity: " + e.Message);
			}
			//Debug.LogWarning($"[ADVANT] RegisterActivity runs for {(DateTime.UtcNow - start).TotalMilliseconds} ms");
			return isSessionNew;
		}
		
		private bool TryUpdateSessionCount()
		{
			ref var session = ref _sessions.GetSession();
			if (session.LastActivity != default(DateTime) && DateTime.UtcNow.Subtract(session.LastActivity) > TimeSpan.FromMinutes(10))
			{
				_sessions.NewSession();
				_sessions.SetUserSessionCount(session.SessionCount + 1);
				
				if (_sessions.GetSession().Area == 0)
					_sessions.SetCurrentArea(session.Area);
				if (string.IsNullOrEmpty(_sessions.GetSession().AbMode))
					_sessions.SetCurrentAbMode(session.AbMode);
				return true;
			}
			return false;
		}

        public void SaveCacheLocally()
        {
			try
			{
				Debug.LogWarning("[ADVANAL] Saving cache locally");
				SerializeSessions();
				SerializeEvents();
				SerializeProperties();
				Debug.LogWarning("[ADVANAL] Serialization is done");
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
			_userId = id;
            await RunSendingLoop();
        }
		
		private void SerializeEvents()
        {
            Serialize<GameEventsPool>(_eventsPath, _events);
        }

        private void SerializeProperties()
        {
            Serialize<GamePropertiesPool>(_propsPath, _properties);
        }
		
		private void SerializeSessions()
        {
			//var start = DateTime.UtcNow;
			//Debug.LogWarning($"[ADVANT] SerializeSessions");
			// if (_userId != -1)
				// RegisterActivity();
			//Debug.LogWarning($"[ADVANT] Activity is registered, start serializing");
            Serialize<GameSessionsPool>(_sessionsPath, _sessions);
			//Debug.LogWarning($"[ADVANT] SerializeSessions runs for {(DateTime.UtcNow - start).TotalMilliseconds} ms");
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

        private async UniTask RunSendingLoop()
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
					
				Debug.LogWarning("[ADVANAL] SENDING ANALYTICS DATA");

				int eventsBatchSize 		= _events.GetCurrentBusyCount();
				int propertiesBatchSize 	= _properties.GetCurrentBusyCount();
				int sessionsBatchSize 		= _sessions.GetCurrentBusyCount();
				
				if (await RegisterActivity() is var isSessionNew && isSessionNew)
					NewEvent("logged_in");
				
				var eventsSending 		= _backend.SendToServerAsync<GameEvent>(await _events.ToJsonAsync(_userId));					
				var propertiesSending 	= _backend.SendToServerAsync<GameProperty>(await _properties.ToJsonAsync(_userId));
				var sessionSending		= _backend.SendToServerAsync<Session>(await _sessions.ToJsonAsync(_userId));
						
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
				if (hasSessionSendingSucceeded)
				{
					//Debug.LogWarning("[ADVANAL] Clear sessions");
					_sessions.FreeFromBeginning(sessionsBatchSize);
				}
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
	
	public static class ParametersArrayExtension
	{
		public static bool Contains(this string[] arr, string value)
		{
			foreach (var p in arr)
			{
				if (p == value)
					return true;
			}
			return false;
		}
	}
}
