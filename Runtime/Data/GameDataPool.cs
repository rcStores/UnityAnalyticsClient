using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using Cysharp.Threading.Tasks;

using Advant.Data.Models;

namespace Advant.Data
{
	[Serializable]
	internal abstract class GameDataPool<T>
	{     
		protected T[] 	_pool;
		protected int[] 	_indices;		
		protected int 	_currentCount;
		
		protected StringBuilder _sb;
		
		protected const int CRITICAL_SIZE_RESTRICTION 	= 10000;
		protected const int INITIAL_SIZE 				= 500;
		protected const int SERIALIZATION_BREAKPOINT 	= 10;
		
		public abstract UniTask<string> ToJsonAsync(long userId);

		public GameDataPool()
		{
			_pool = new T[INITIAL_SIZE];

			_currentCount = 0;
			_indices = new int[INITIAL_SIZE];	
			for (int i = 0; i < INITIAL_SIZE; ++i)
			{
				_indices[i] = i;
			}
			
			_sb = new StringBuilder();
		}

		public ref T NewElement()
		{
			if (_currentCount == _pool.Length)
			{
				_currentCount = 0;
				// if (_pool.Length * 2 >= CRITICAL_SIZE_RESTRICTION)
				// {
					// // the server is down for too long
					// --_currentCount;
				// }
				// else
				// {
					// ExtendPool();
				// }	
			}
			
			return ref _pool[_indices[_currentCount++]];
		}

		public virtual void FreeFromBeginning(int count)
		{
			if (count > _pool.Length)
				count = _pool.Length;
			
			for (int i = 0; i < count; ++i)
			{
				(_indices[i], _indices[_currentCount - 1 - i]) = (_indices[_currentCount - 1 - i], _indices[i]); // swap indices
			}
			_currentCount = _currentCount - count;
		}

		public int GetCurrentBusyCount()
		{
			return _currentCount;
		}
		
		private void ExtendPool()
		{
			Debug.LogWarning("ExtendingPool");
			try
			{
				Array.Resize(ref _pool, 	_pool.Length * 2);
				Array.Resize(ref _indices, 	_indices.Length * 2);
				
				for (int i = _currentCount; i < _indices.Length; ++i)
				{
					_indices[i] = i;
				}
			}
			catch (Exception e)
			{
				Debug.LogError("Pool allocation failure: " + e.Message);
				return;
			}
		}
	}
	
	[Serializable]
	internal class GamePropertiesPool : GameDataPool<GameProperty>
	{
		public override async UniTask<string> ToJsonAsync(long userId)
		{
			string result = null;
			
			if (_currentCount == 0)
				return result;
			
			try
			{
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
				Debug.LogError("Game properties JSON serialization failed.");
			}
			
			return result;
		}
	}
	
	[Serializable]
	internal class GameEventsPool : GameDataPool<GameEvent>
	{
		public override async UniTask<string> ToJsonAsync(long userId)
		{
			string result = null;
			
			if (_currentCount == 0)
			{
				Debug.LogWarning("[ADVANT] empty events pool");
				return result;
			}
			
			try
			{	
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
				Debug.LogError("Game events JSON serialization failed.");
			}
			//Debug.LogWarning("[ADVANT] Events in JSON:\n " + result);
			return result;
		}
	}
	
	[Serializable]
	internal class GameSessionsPool : GameDataPool<Session>
	{
		private string 		_abMode;
		private long 		_currentSessionCount;
		private int 		_gameArea;
		private DateTime	_sessionStart;
		
		public override async UniTask<string> ToJsonAsync(long userId)
		{
			string result = null;
			
			if (_currentCount == 0)
				return result;
			
			try
			{
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
				Debug.LogError("Game session JSON serialization failed.");
			}
			
			return result;
		}
		
		public void NewSession(long newSessionCount)
		{
			if (_currentCount > 0)
			{
				ref var prevSession = _pool[_indices[_currentCount - 1]];
				_abMode = prevSession.AbMode;
				_currentSessionCount = prevSession.SessionCount;
				_gameArea = prevSession.Area;
			}
			
			ref var s = ref NewElement();
			s.AbMode = _abMode;
			s.Area = _gameArea;
			s.SessionStart	= _sessionStart = RealDateTime.UtcNow;
			s.LastActivity	= RealDateTime.UtcNow;
			s.SessionId = Guid.NewGuid().ToString();
			Debug.LogWarning($"[ADVANT] Cached session count = {_currentSessionCount}");
			if (newSessionCount == 0)
				s.SessionCount = ++_currentSessionCount;
			else
				s.SessionCount = _currentSessionCount = newSessionCount;
			Debug.LogWarning($"[ADVANT] NEW SESSION:\nab_mode = {s.AbMode}\narea = {s.Area}\nstart = {s.SessionStart}\nend = {s.LastActivity}\ncount = {s.SessionCount}");
			
						
			//return s.SessionId;
		}
		
		public bool RegisterActivity()
		{
			CurrentSession().LastActivity = RealDateTime.UtcNow;
		}
		
		public ref Session CurrentSession() 
		{
			if (_currentCount == 0)
				NewSession();
			 
			return ref _pool[_indices[_currentCount - 1]];
		}

		public override void FreeFromBeginning(int count)
		{			
			//base.FreeFromBeginning(count - 1);
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
		}
		
		// public void ClearLastSession() => 
			// _currentCount = _currentCount > 1 ? _currentCount - 1 : _currentCount;
			
		public void SetSessionStart()
		{
			_sessionStart = RealDateTime.UtcNow;
			if (_currentCount != 0)
				_pool[_indices[_currentCount - 1]].SessionStart = _sessionStart;
		}
		
		public void SetCurrentAbMode(string mode)
		{
			_abMode = mode;
			if (_currentCount != 0)
				_pool[_indices[_currentCount - 1]].AbMode = _abMode;
		}
		
		public void SetUserSessionCount(long count)	
		{
			_userSessionCount = count;
			if (_currentCount != 0)
				_pool[_indices[_currentCount - 1]].SessionCount = _userSessionCount;
		}
		
		public void SetCurrentArea(int area)
		{
			_gameArea = area;
			if (_currentCount != 0)
				_pool[_indices[_currentCount - 1]].Area = _gameArea;
		}
		
		public int GetCurrentArea()	=> _gameArea;
	}

} // namespace Advant.Data
	