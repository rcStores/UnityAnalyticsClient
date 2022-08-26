using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using Cysharp.Threading.Tasks;

using Advant.Data.Models;

namespace Advant.Data.Models
{
	
internal interface IGameData
{
	//string Name { get; }
	//string Table { get; }
	public void ToJson(long id, StringBuilder sb);
	public void Free();
}

internal struct Value
{
	internal enum EValueType
	{
		Int, Float, Double, String, Bool, DateTime
	}

	private string _name;
	private string _value;
	private EValueType _type;

	public void Set(string name, long value)
	{
		_name = name;
		_value = $"\"{value}\"";
		_type = EValueType.Int;
	}

	public void Set(string name, double value)
	{
		_name = name;
		_value = $"\"{value}\"";
		_type = EValueType.Double;
	}

	public void Set(string name, bool value)
	{
		_name = name;
		_value = $"\"{value}\"";
		_type = EValueType.Bool;
	}

	public void Set(string name, DateTime value)
	{
		_name = name;
		_value = $"\"{value.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture)}\"";
		_type = EValueType.DateTime;
	}

	public void Set(string name, string value)
	{
		_name = name;
		_value = value is null ?
			"null" :
			$"\"{value.Replace(Environment.NewLine, @"\\n")}\"";
		_type = EValueType.String;
	}

	public void ToJson(StringBuilder sb)
	{
		sb.Append($"{{\"name\":\"{_name}\", \"value\":{_value}, \"type\":{(int)_type}}}");
	}
}

	[Serializable]
	public struct GameEvent : IGameData
	{
		private int _maxParameterCount;
		private int _currentCount;

		private int _id;
		private string _name;
		private string _timestamp;

		private Value[] _parameters;

		public void SetName(string name)
		{
			_name = name;
		}

		public void SetId(int id)
		{
			_id = id;
		}
		
		public void SetMaxParameterCount(int count)
		{
			Array.Resize(ref _parameters, count);
			_maxParameterCount = count;
		}
		
		public void SetTimestamp(DateTime timestamp)
		{
			_timestamp = timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture);
		}

		public void Add(string name, int value)
		{
			if (_currentCount == _maxParameterCount)
				ExtendParameterPool();
			_parameters[_currentCount++].Set(name, value);
		}

		public void Add(string name, double value)
		{
			if (_currentCount == _maxParameterCount)
				ExtendParameterPool();
			_parameters[_currentCount++].Set(name, value);
		}

		public void Add(string name, bool value)
		{
			if (_currentCount == _maxParameterCount)
				ExtendParameterPool();
			_parameters[_currentCount++].Set(name, value);
		}

		public void Add(string name, DateTime value)
		{
			if (_currentCount == _maxParameterCount)
				ExtendParameterPool();
			_parameters[_currentCount++].Set(name, value);
		}
		
		public void Add(string name, string value)
		{
			if (_currentCount == _maxParameterCount)
				ExtendParameterPool();
			_parameters[_currentCount++].Set(name, value);
		}
		
		public void Free()
		{
			_currentCount = 0;
		}

		public void ToJson(long id, StringBuilder sb)
		{
			sb.Append($"{{\"user_id\":{id}, \"name\":\"{_name}\", \"event_time\":\"{_timestamp}\", \"current_app_version\":\"{Application.version}\", \"parameters\":");
			_parameters.ToJson(sb, _currentCount);
			sb.Append('}');
		}
		
		private void ExtendParameterPool()
		{
			var newMaxCount = _maxParameterCount * 2;
			Value[] newParameters = null;
			try
			{
				newParameters = new Value[newMaxCount];
			}
			catch (Exception e)
			{
				Debug.LogError($"Cannot add more parameters to the event (new count = {newMaxCount}): {e.Message}");
			}
			for (int i = 0; i < _currentCount; ++i)
			{
				newParameters[i] = _parameters[i];
			}
			_parameters = newParameters;
		}
	}
	
	[Serializable]
	internal struct GameProperty : IGameData
	{
		private string _table;
		private Value _value;
		
		public void Set(string name, int value)
		{
			_value.Set(name, value);
		}

		public void Set(string name, double value)
		{
			_value.Set(name, value);
		}

		public void Set(string name, string value)
		{
			_value.Set(name, value);
		}

		public void Set(string name, bool value)
		{
			_value.Set(name, value);
		}
		
		public void Set(string name, DateTime value)
		{
			_value.Set(name, value);
		}
		
		public void SetTableName(string table)
		{
			_table = table;
		}
		
		public void Free() { }
		
		public void ToJson(long id, StringBuilder sb)
		{
			sb.Append($"{{\"table\":\"{_table}\", \"user_id\":{id}, \"value\":");
			_value.ToJson(sb);
			sb.Append('}');
		}
	}
	
} // namespace Advant.Data.Models

namespace Advant.Data
{
	[Serializable]
	internal class SimplePool<T> where T : IGameData
	{     
		private T[] _pool; 
		private int _poolCount;

		private int[] _busyIdxs;		
		private int[] _freeIdxs;
		private int _currentCount;

		private StringBuilder _sb;
		
		private const int CRITICAL_SIZE_RESTRICTION = 10000;
		private const int MAX_GAME_EVENT_PARAMETER_COUNT = 10;

		public SimplePool(int maxSize)
		{
			_poolCount = 0;
			_pool = new T[maxSize];

			_currentCount = 0;
			_busyIdxs = new int[maxSize];
			_freeIdxs = new int[maxSize];	
			for (int i = 0; i < maxSize; ++i)
			{
				_freeIdxs[i] = i;
			}

			_sb = new StringBuilder();
		}
		
		private void ExtendPool()
		{
			Debug.LogWarning("ExtendingPool");
			try
			{
				Array.Resize(ref _pool, _pool.Length * 2);
				Array.Resize(ref _busyIdxs, _busyIdxs.Length * 2);
				Array.Resize(ref _freeIdxs, _freeIdxs.Length * 2);
			}
			catch (Exception e)
			{
				Debug.LogError("Pool allocation failure: " + e.Message);
				return;
			}
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
			
			int idx = _freeIdxs[_currentCount];
			_busyIdxs[_currentCount++] = idx;
			ref var element = ref _pool[idx];
			element.Free();

			return ref element;
		}

		public void FreeFromBeginning(int count)
		{			
			for (int i = 0, j = count; i < count || j < _currentCount; ++i, ++j)
			{
				if (i < count) 
				{
					_freeIdxs[_currentCount - 1 - i] = _busyIdxs[i];
				}
				
				if (j < _currentCount)
				{
					_busyIdxs[i] = _busyIdxs[j];
				}
			}
			_currentCount = _currentCount - count;
		}

		public async UniTask<string> ToJson(long userId)
		{
			if (_currentCount == 0)
				return null;
			
			int breakPointCount = 10;
			
			_sb.Append('[');
			for (int i = 0; i < _currentCount; ++i)
			{
				if (i > 0)
					_sb.Append(',');
				
				if (i % breakPointCount == 0)
				{
					await UniTask.Delay(20, false, PlayerLoopTiming.PostLateUpdate);
				}
				
				_pool[_busyIdxs[i]].ToJson(userId, _sb);	
			}
			string result = _sb.Append(']').ToString();
			_sb.Clear();
			return result;
		}

		// public void MarkAsBusy(int idx)
		// {
			// _busyIdxs[_poolCount] = idx;
		// }

		public int GetCurrentBusyCount()
		{
			return _currentCount;
		}
	}
	
	internal static class ParametersExtensions
	{
		public static void ToJson(this Value[] list, StringBuilder sb, int count)
		{
			Debug.LogWarning("ParametersExtensions.ToJson: parameters count = " + count);
			sb.Append('[');
			if (list != null && count != 0)
			{
				for (int i = 0; i < count; ++i)
				{
					list[i].ToJson(sb);
					sb.Append(',');
				}
				sb.Remove(sb.Length - 1, 1);
			}
			sb.Append(']');
		}
	}
} // namespace Advant.Data
	