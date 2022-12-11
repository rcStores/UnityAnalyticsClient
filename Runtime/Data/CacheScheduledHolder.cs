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
		private readonly NetworkTimeHolder 		_timeHolder;		
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
		private const int SESSION_TIMEOUT				= 10; // in minutes

        public CacheScheduledHolder(string usersTableName, Backend backend, NetworkTimeHolder timeHolder)
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
			_timeHolder = timeHolder;
			
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
		
		// public async UniTask RefreshAsync()
		// {
			// if (RealDateTime.UtcNow.Subtract(_sessions.CurrentSession().LastActivity) > TimeSpan.FromMinutes(10))
			// {
				// if (!await _backend.PutSessionCount(_userId, NewSession().SessionCount))
				// {
					// Debug.LogWarning("[ADVANAL] PutSessionCount returns false");
					// _sessions.CurrentSession().Unregistered = true;
				// }
				// NewEvent("logged_in");
				// Debug.LogWarning("[ADVANAL] logged_in was added to the events batch");
			// }
			// else
				// _sessions.RegisterActivity();
		// }
		
		public async UniTask StartOrContinueSessionAsync(DateTime start, long dbSessionCount = 0)
		{
			EnsureTimestampsValid(start);
			
			//Debug.LogWarning($"[ADVANAL] Restoring or creating a session. Current session count according to database: {dbSessionCount}");
			if (!_sessions.HasCurrentSession())
			{
				NewSession(start, dbSessionCount);
				_sessions.CurrentSession().Unregistered = false;
				NewEvent("logged_in", hasValidTimestamps: true).Time = start;
			}
			else if ((start - _sessions.CurrentSession().LastActivity).TotalMinutes >= SESSION_TIMEOUT)
			{
				//Debug.LogWarning("[ADVANAL] prev session's last activity was more than 10 minutes ago - starting a new one.");
				if (await _backend.PutSessionCount(_userId, NewSession(start, dbSessionCount).SessionCount))
				{
					//Debug.LogWarning("[ADVANAL] PutSessionCount returns true");
					_sessions.CurrentSession().Unregistered = false;
				}
				NewEvent("logged_in", hasValidTimestamps: true).Time = start;
				//Debug.LogWarning("[ADVANAL] logged_in was added to the events batch, event_time = " + start);
			}
			else
			{
				if (_sessions.CurrentSession().SessionCount + 1 == dbSessionCount)
				{
					await _backend.PutSessionCount(_userId, _sessions.CurrentSession().SessionCount);
					//Debug.LogWarning($"[ADVANAL] Session that was registered on the app start is cancelled since the user continues the previous session with number {_sessions.CurrentSession().SessionCount}");
				}
				else if (dbSessionCount == 1)
				{
					//Debug.LogWarning($"[ADVANAL] Sessions was dropped - create new {dbSessionCount}st session");
					NewSession(start, dbSessionCount);
					_sessions.CurrentSession().Unregistered = false;
					NewEvent("logged_in", hasValidTimestamps: true).Time = start;
				}
				
				_sessions.RegisterActivity();
			}
			
			
		}
		
		private void EnsureTimestampsValid(DateTime current, long dbSessionCount = 0)
		{
			//Debug.LogWarning("[ADVANAL] Look for invalid events...");
			var (brokenBatchSize, firstIdx) = _events.GetInvalidEventsCount(current, _timeHolder);
			
			if (brokenBatchSize == 0) return;
			
			//Debug.LogWarning($"[ADVANAL] There are {brokenBatchSize} invalid events. Recalculating timestamps...");
			
			DateTime sessionStart, sessionEnd = current.AddMinutes(-10);
			var sessionDuration = TimeSpan.FromSeconds(brokenBatchSize * 5);
			if (_sessions.HasCurrentSession())
			{
				//Debug.LogWarning("[ADVANAL] There was prev session");
				ref var session = ref _sessions.CurrentSession();
				
				if (session.LastValidTimestamp == default)
				{
					//Debug.LogWarning("[ADVANAL] ... with no valid timestamps. Create a session for the batch");
					sessionStart = (sessionEnd - sessionDuration).AddSeconds(-5);
					//Debug.LogWarning($"[ADVANAL] SessionStart = {sessionStart}, SessionEnd = {sessionEnd}");
					ref var newSession =  ref NewSession(sessionStart, dbSessionCount);
					newSession.LastActivity = newSession.LastValidTimestamp = sessionEnd;
					newSession.HasValidTimestamps = true;
					NewEvent("logged_in", hasValidTimestamps: true).Time = sessionStart;
				}
				else
					 sessionStart = session.LastValidTimestamp.AddMinutes(10);
				
				if (sessionEnd - sessionStart < sessionDuration)
				{
					//Debug.LogWarning("[ADVANAL] The time interval between two known sessions is too small - fit the batch here");
					sessionStart = session.LastValidTimestamp.AddSeconds(5);
					sessionEnd = sessionStart.AddSeconds(sessionDuration.TotalSeconds);
					if (sessionEnd >= current)
					{
						sessionEnd = current.AddSeconds(-5);
						//Debug.LogWarning($"[ADVANAL] Distribute events up to current time. SessionEnd = {sessionEnd}");
					}
					// else
						// Debug.LogWarning($"[ADVANAL] Distribute events up to calculated duration. SessionEnd = {sessionEnd}");
						
					//Debug.LogWarning($"[ADVANAL] session.LastActivity = = {sessionEnd}");
					session.LastValidTimestamp = session.LastActivity = sessionEnd;
					session.HasValidTimestamps = true;
				}
				else
				{
					//Debug.LogWarning("[ADVANAL] The time interval between two known sessions is big enough to place one session, so create it");
					ref var newSession =  ref NewSession(sessionStart, dbSessionCount);
					sessionEnd = sessionStart.AddSeconds(sessionDuration.TotalSeconds);
					//Debug.LogWarning($"[ADVANAL] SessionStart = {sessionStart}, SessionEnd = {sessionEnd}");
					newSession.LastActivity = sessionEnd; // LastValidTimestamp
					newSession.HasValidTimestamps = true;
					NewEvent("logged_in", hasValidTimestamps: true).Time = sessionStart;
				}
			}
			else
			{
				//Debug.LogWarning("[ADVANAL] There was no session yet - create one");
				sessionStart = sessionEnd - sessionDuration;
				//Debug.LogWarning($"[ADVANAL] SessionStart = {sessionStart}, SessionEnd = {sessionEnd}");
				ref var newSession =  ref NewSession(sessionStart, dbSessionCount);
				newSession.LastActivity = newSession.LastValidTimestamp = sessionEnd;
				newSession.HasValidTimestamps = true;
				NewEvent("logged_in", hasValidTimestamps: true).Time = sessionStart;
			}
			_events.ValidateBrokenBatch(sessionStart, sessionEnd, brokenBatchSize, firstIdx);	
		}
		
		public void SetUserId(long id) => _userId = id;

#region Events

		public ref GameEvent NewEvent(string eventName, bool hasValidTimestamps = false) => ref NewEventImpl(eventName, hasValidTimestamps);
		
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
				
		private ref GameEvent NewEventImpl(string eventName, bool hasValidTimestamps = false)
		{
			ref GameEvent e = ref _events.NewElement();
			
			e.Free();
			e.SetTimestamp(DateTime.UtcNow);
			e.SetMaxParameterCount(GAME_EVENT_PARAMETER_COUNT);
			e.SetName(eventName);
			e.HasValidTimestamps = hasValidTimestamps;
			//Debug.LogWarning($"[ADVANT] New event, name = {e.Name}, timestamp = {e.Time}");
			
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
		
#endregion

#region Properties

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

#endregion

#region Sessions

		public void SetSessionStart() => _sessions.SetSessionStart();
		
		public ref Session NewSession(DateTime start, long dbSessionCount = 0)
		{
			ref var s = ref _sessions.NewSession(dbSessionCount);
			s.SessionStart = start;
			s.GameVersion = Application.version;
			if (_userId == 1)
				AnalEventer.LogAdvantDebugMessage("cache_holder_user_id_not_set");
			SetGlobalEventParam("session_id", $"{_userId}_{s.SessionCount}");
			return ref s;
		}
		
		public long GetSessionCount() => _sessions.HasCurrentSession() ? _sessions.CurrentSession().SessionCount : -1;
		
#endregion

#region Sending loop

		public async UniTask StartSendingDataAsync()
        {
			Log.Info("Start scheduler");
            Debug.Assert(_userId != -1);
            await RunSendingLoop();
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
					
				//Debug.LogWarning("[ADVANAL] SENDING ANALYTICS DATA");
				
				_sessions.RegisterActivity();
				
				if (!_timeHolder.IsServerReached) continue;
				
				int eventsBatchSize 		= _events.GetCurrentBusyCount();
				int propertiesBatchSize 	= _properties.GetCurrentBusyCount();
				int sessionsBatchSize 		= _sessions.GetCurrentBusyCount();
				
				var eventsSending 		= _backend.SendToServerAsync<GameEvent>(await _events.ToJsonAsync(_userId, _timeHolder));					
				var propertiesSending 	= _backend.SendToServerAsync<GameProperty>(await _properties.ToJsonAsync(_userId));
				var sessionSending		= _backend.SendToServerAsync<Session>(await _sessions.ToJsonAsync(_userId, _timeHolder));
						
				var (eventsSendingResult, propertiesSendingResult, sessionsSendingResult) = 
					await UniTask.WhenAll(eventsSending, propertiesSending, sessionSending);
				
				AnalEventer.LogAdvantDebugDataSending("events", 
													eventsBatchSize, 
													eventsSendingResult.IsSuccess, 
													eventsSendingResult.StatusCode, 
													eventsSendingResult.RequestError, 
													eventsSendingResult.ExceptionMessage);
				AnalEventer.LogAdvantDebugDataSending("properties", 
													propertiesBatchSize, 
													propertiesSendingResult.IsSuccess, 
													propertiesSendingResult.StatusCode, 
													propertiesSendingResult.RequestError, 
													propertiesSendingResult.ExceptionMessage);
				AnalEventer.LogAdvantDebugDataSending("sessions", 
													sessionsBatchSize, 
													sessionsSendingResult.IsSuccess, 
													sessionsSendingResult.StatusCode, 
													sessionsSendingResult.RequestError, 
													sessionsSendingResult.ExceptionMessage);
													
				//Debug.LogWarning("[ADVANAL] Getting results of data sending...");
				
				if (eventsSendingResult.IsSuccess) 
				{
					//Debug.LogWarning("[ADVANAL] Clear events");
					_events.FreeFromBeginning(eventsBatchSize);
				}
				if (propertiesSendingResult.IsSuccess)
				{
					//Debug.LogWarning("[ADVANAL] Clear properties");
					_properties.FreeFromBeginning(propertiesBatchSize);
				}
				if (sessionsSendingResult.IsSuccess)
				{
					//Debug.LogWarning("[ADVANAL] Clear sessions, batch size = " + sessionsBatchSize);
					_sessions.FreeFromBeginning(sessionsBatchSize);
				}
			}
        }

#endregion	

#region Serialization

		public void SaveCacheLocally()
        {
			try
			{
				//Debug.LogWarning("[ADVANAL] Saving cache locally");
				SerializeSessions(); 
				SerializeEvents();
				SerializeProperties();
				//Debug.LogWarning("[ADVANAL] Serialization is done");
			}
			catch (Exception e)
			{
				AnalEventer.LogAdvantDebugFailure("cache_saving_failure", e);
				Debug.LogWarning("Saving cache failure: " + e.Message);
			}
        }
		
		private void SerializeEvents()
        {
			_events.ValidateTimestamps(_timeHolder);
            Serialize<GameEventsPool>(_eventsPath, _events);
        }

        private void SerializeProperties()
        {
			_properties.ValidateTimestamps(_timeHolder);
            Serialize<GamePropertiesPool>(_propsPath, _properties);
        }
		
		private void SerializeSessions()
        {
			_sessions.RegisterActivity();
			_sessions.ValidateTimestamps(_timeHolder);
            Serialize<GameSessionsPool>(_sessionsPath, _sessions);
        }
		

        private void Serialize<T>(string filePath, T data)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            try
            {
				using var fs = new FileStream(filePath, FileMode.OpenOrCreate);
                formatter.Serialize(fs, data);
            }
            catch (Exception e)
            {
				AnalEventer.LogAdvantDebugFailure("serialize_failure", e, typeof(T));
                Log.Info("Failed to serialize. Reason: " + e.Message);
            }
        }
		
		private TPool Deserialize<TPool>(string filePath) where TPool : new()
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
					AnalEventer.LogAdvantDebugFailure("deserialize_failure", e, typeof(TPool));
					result = new TPool();
				}
				finally
				{
					File.Delete(filePath);
				}
			}
			
			return result;
        }
		
#endregion

#region Complex setters (several analytic data types etc.)

		public void SetCurrentArea(int area)
		{
			_sessions.SetCurrentArea(area);
			SetGlobalEventParam("area", area);
		}
		
		public void SetCurrentAbMode(string abMode, string propertyTableName)
		{
			_sessions.SetCurrentAbMode(abMode);	
			SetGlobalEventParam("ab_mode", abMode);
			NewProperty("current_ab_mode", abMode, propertyTableName);
		}

#endregion
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
