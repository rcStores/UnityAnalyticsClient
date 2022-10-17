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
		internal T[] Elements { get => _pool; }
		
		internal T[] 	_pool;
		protected int[] 	_indices;		
		protected int 	_currentCount;
		
		protected StringBuilder _sb;
		
		protected const int CRITICAL_SIZE_RESTRICTION 	= 10000;
		protected const int INITIAL_SIZE 				= 500;
		protected const int SERIALIZATION_BREAKPOINT 	= 10;
		
		public abstract UniTask<string> ToJsonAsync(long userId, NetworkTimeHolder timeHolder = null);
		public abstract void 			ValidateTimestamps(NetworkTimeHolder timeHolder);

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
		public override async UniTask<string> ToJsonAsync(long userId, NetworkTimeHolder timeHolder = null)
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
					timeHolder?.ValidateTimestamps(ref _pool[_indices[i]]);
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
		
		public override void ValidateTimestamps(NetworkTimeHolder timeHolder)
		{
			for (int i = 0; i < _currentCount; ++i)
			{
				timeHolder.ValidateTimestamps(ref _pool[_indices[i]]);
			}
		}
	}
	
	[Serializable]
	internal class GameEventsPool : GameDataPool<GameEvent>
	{
		public override async UniTask<string> ToJsonAsync(long userId, NetworkTimeHolder timeHolder = null)
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
					timeHolder?.ValidateTimestamps(ref _pool[_indices[i]]);
					_pool[_indices[i]].ToJson(userId, _sb);	
				}
				result = _sb.Append(']').ToString();
				_sb.Clear();
			}
			catch (Exception e)
			{
				Debug.LogError("Game events JSON serialization failed: " + e.Message);
			}
			//Debug.LogWarning("[ADVANT] Events in JSON:\n " + result);
			return result;
		}
		
		public override void ValidateTimestamps(NetworkTimeHolder timeHolder)
		{
			for (int i = 0; i < _currentCount; ++i)
			{
				timeHolder.ValidateTimestamps(ref _pool[_indices[i]]);
			}
		}
		
		public (int, int) GetInvalidEventsCount(DateTime currentInitialTime, NetworkTimeHolder timeHolder)
		{
			if (_currentCount == 0) return (0, -1);
			
			int batchSize = 0;
			int firstIdx = -1;
			for (int i = 0; i < _currentCount; ++i)
			{
				var eventTime = timeHolder.GetVerifiedTime(_pool[_indices[i]].Time);
				if (!_pool[_indices[i]].HasValidTimestamps &&  !(eventTime > currentInitialTime && eventTime < timeHolder.GetVerifiedTime(DateTime.UtcNow)))
				{
					batchSize++;
					if (firstIdx != -1)
						firstIdx = i;
				}
			}
			return (batchSize, firstIdx);
		}
		
		public void ValidateBrokenBatch(DateTime firstEventTime, DateTime lastEventTime, int batchSize, int firstIdx)
		{
			bool isRecalculationNeeded = false;
			bool firstInvalidTimestamp = true;
			for (int i = firstIdx; i < batchSize; ++i)
			{
				if (!_pool[_indices[i]].HasValidTimestamps && (_pool[_indices[i]].Time > lastEventTime || _pool[_indices[i]].Time < firstEventTime))
					isRecalculationNeeded = true;
			}
			
			if (isRecalculationNeeded)
			{
				_pool[_indices[firstIdx]].Time = firstEventTime;
				Debug.LogWarning($"[ADVANT] {_pool[_indices[firstIdx]].Name}'s new timestamp = {_pool[_indices[firstIdx]].Time}");
			}
			_pool[_indices[firstIdx]].HasValidTimestamps = true;

			var step = (lastEventTime - firstEventTime) / batchSize;
			for (int i = firstIdx + 1; i < batchSize; ++i)
			{
				if (isRecalculationNeeded)
				{
					_pool[_indices[i]].Time = _pool[_indices[i - 1]].Time.AddSeconds(step.TotalSeconds);
					Debug.LogWarning($"[ADVANT] {_pool[_indices[i]].Name}'s new timestamp = {_pool[_indices[i]].Time}");
				}
				_pool[_indices[i]].HasValidTimestamps = true;
				
				if (_pool[_indices[i]].Time == lastEventTime)
					Debug.LogWarning("[ADVANT] Last timestamp is reached");
			}
		}
	}
} // namespace Advant.Data
	