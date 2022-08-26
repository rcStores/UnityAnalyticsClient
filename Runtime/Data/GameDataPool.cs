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
		private T[] 	_pool;
		private int[] 	_indices;		
		private int 	_currentCount;
		
		private StringBuilder _sb;
		
		private const int CRITICAL_SIZE_RESTRICTION 		= 10000;
		private const int MAX_GAME_EVENT_PARAMETER_COUNT 	= 10;
		private const int SERIALIZATION_BREAKPOINT 			= 10;
		
		public abstract async UniTask<string> ToJsonAsync(long userId);

		public SimplePool(int maxSize)
		{
			_pool = new T[maxSize];

			_currentCount = 0;
			_indices = new int[maxSize];	
			for (int i = 0; i < maxSize; ++i)
			{
				_indices[i] = i;
			}
			
			_sb = new StringBuilder();
		}

		public ref T NewElement()
		{
			if (_currentCount >= _pool.Length)
			{
				if (_pool.Length * 2 >= CRITICAL_SIZE_RESTRICTION)
				{
					// the server is down for too long
					--_currentCount;
				}
				else
				{
					ExtendPool();
				}	
			}
			
			return ref _pool[_indices[_currentCount++]];
		}

		public void FreeFromBeginning(int count)
		{			
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
			if (_currentCount == 0)
				return null;
			
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
			string result = _sb.Append(']').ToString();
			_sb.Clear();
			return result;
		}
	}
	
	[Serializable]
	internal class GameEventsPool : GameDataPool<GameEvent>
	{
		public override async UniTask<string> ToJsonAsync(long userId)
		{
			if (_currentCount == 0)
				return null;
			
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
			string result = _sb.Append(']').ToString();
			_sb.Clear();
			return result;
		}
	}

} // namespace Advant.Data
	