using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using Cysharp.Threading.Tasks;

using Advant.Data.Models;
using Advant;

namespace Advant.Data
{	
	[Serializable]
	internal class GameSessionsPool : GameDataPool<Session>
	{
		private string 		_abMode;
		private long 		_currentSessionCount;
		private int 		_gameArea;
		private DateTime	_sessionStart;
		
#region Setters
			
		public void SetSessionStart()
		{
			_sessionStart = DateTime.UtcNow;
			if (_currentCount != 0)
				_pool[_indices[_currentCount - 1]].SessionStart = _sessionStart;
		}
		
		public void SetCurrentAbMode(string mode)
		{
			_abMode = mode;
			if (_currentCount != 0)
				_pool[_indices[_currentCount - 1]].AbMode = _abMode;
		}
		
		public void SetCurrentArea(int area)
		{
			_gameArea = area;
			if (_currentCount != 0)
				_pool[_indices[_currentCount - 1]].Area = _gameArea;
		}
		
#endregion
		
#region Sending routines

		public override async UniTask<string> ToJsonAsync(long userId, NetworkTimeHolder timeHolder = null)
		{
			string result = null;
			
			if (_currentCount == 0)
				return result;
			
			try
			{
				ValidateTimestamps(timeHolder);				
				_sb.Append('[');
				for (int i = 0; i < _currentCount; ++i)
				{
					if (i > 0)
						_sb.Append(',');
			
					if (i % SERIALIZATION_BREAKPOINT == 0)
					{
						await UniTask.Delay(20, false, PlayerLoopTiming.PostLateUpdate);
					}
					_pool[_indices[i]].ToJson(userId, _sb);	
				}
				result = _sb.Append(']').ToString();
				_sb.Clear();
			}
			catch (Exception e)
			{
				AdvAnalytics.LogFailureToDTD("json_serialization_failure", e, typeof(Session));
				Debug.LogError("Game session JSON serialization failed.");
			}
			
			return result;
		}
		
		public override void FreeFromBeginning(int count)
		{			
			base.FreeFromBeginning(count - 1);
			/* Logged version
			count--;
			if (count > _pool.Length)
				count = _pool.Length;
			Debug.LogWarning("Pool elements:");
			Debug.LogWarning($"Current: Start = {CurrentSession().SessionStart}, LastActivity = {CurrentSession().LastActivity}");
			for (int i = 0; i < count; ++i)
			{
				Debug.LogWarning($"i = {i}: Start = {_pool[_indices[i]].SessionStart}, LastActivity = {_pool[_indices[i]].LastActivity}");
				Debug.LogWarning($"_currentCount - 1 - i = {_currentCount - 1 - i}: Start = {_pool[_indices[_currentCount - 1 - i]].SessionStart}, LastActivity = {_pool[_indices[_currentCount - 1 - i]].LastActivity}\nSwap...");
				(_indices[i], _indices[_currentCount - 1 - i]) = (_indices[_currentCount - 1 - i], _indices[i]); // swap indices
				Debug.LogWarning($"After swap:\ni = {i}: Start = {_pool[_indices[i]].SessionStart}, LastActivity = {_pool[_indices[i]].LastActivity}");
				Debug.LogWarning($"_currentCount - 1 - i = {_currentCount - 1 - i}: Start = {_pool[_indices[_currentCount - 1 - i]].SessionStart}, LastActivity = {_pool[_indices[_currentCount - 1 - i]].LastActivity}");
			}
			Debug.LogWarning($"Current (CurrentSession): Start = {CurrentSession().SessionStart}, LastActivity = {CurrentSession().LastActivity}");
			Debug.LogWarning($"Current (raw): Start = {_pool[_indices[_currentCount - 1]].SessionStart}, LastActivity = {_pool[_indices[_currentCount - 1]].LastActivity}\nRecalculating _currentCount...");
			_currentCount = _currentCount - count;
			Debug.LogWarning($"Current (CurrentSession): Start = {CurrentSession().SessionStart}, LastActivity = {CurrentSession().LastActivity}");
			Debug.LogWarning($"Current (raw): Start = {_pool[_indices[_currentCount - 1]].SessionStart}, LastActivity = {_pool[_indices[_currentCount - 1]].LastActivity}");
			
			ref var session = ref CurrentSession();
			Debug.LogWarning($"session.SessionCount = {session.SessionCount}, session.SessionStart = {session.SessionStart}, session.LastActivity = {session.LastActivity}");
			*/
		}
		
		public override void ValidateTimestamps(NetworkTimeHolder timeHolder)
		{
			if (HasCurrentSession())
				timeHolder?.ValidateTimestamps(ref CurrentSession());
		}

#endregion
				
		public ref Session NewSession(long newSessionCount = 0) 
		{
			if (_currentCount > 0)
			{
				ref var prevSession = ref _pool[_indices[_currentCount - 1]];
				_abMode = prevSession.AbMode;
				_currentSessionCount = prevSession.SessionCount;
				_gameArea = prevSession.Area;
			}
			
			ref var s = ref NewElement();
			s.AbMode = _abMode;
			s.Area = _gameArea;
			s.SessionStart	= _sessionStart = DateTime.UtcNow;
			s.LastActivity	= DateTime.UtcNow;
			s.HasValidTimestamps = false;
			//Debug.LogWarning($"[ADVANT] Prev session count = {_currentSessionCount}. Сomparing it with the new value...");
			
			if (_currentSessionCount + 1 == newSessionCount || newSessionCount == 1)
			{
				//Debug.LogWarning($"[ADVANT] The new value is valid (prev + 1 or 1) - assign it to the new session count, the new session is registered");
				s.SessionCount = _currentSessionCount = newSessionCount;
				s.Unregistered = false;
			}
			else
			{
				//Debug.LogWarning($"[ADVANT] The new value is invalid - restore session count from the prev session, make it unregistered");
				s.SessionCount = ++_currentSessionCount;
				s.Unregistered = true;
			}
			//Debug.LogWarning($"[ADVANT] NEW SESSION:\nab_mode = {s.AbMode}\narea = {s.Area}\nstart = {s.SessionStart}\nend = {s.LastActivity}\ncount = {s.SessionCount}");
				
			return ref s;
		}
		
		public void RegisterActivity() 
		{
			if (HasCurrentSession())
			{
				//Debug.LogWarning($"[ADVANT] Session {CurrentSession().SessionCount}'s last activity: {CurrentSession().LastActivity}");
				CurrentSession().LastActivity = DateTime.UtcNow;
				CurrentSession().HasValidTimestamps = false;
			}
		}
		
		public ref Session CurrentSession() 
		{
			var idx = _currentCount > 0 ? _currentCount - 1 : 0;
			return ref _pool[_indices[idx]];
		}
		
		public bool HasCurrentSession() => _currentCount != 0;
	}	
} // namespace Advant.Data