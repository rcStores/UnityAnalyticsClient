using System;
using System.Globalization;
using System.Text;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace Advant.Data.Models
{
[Serializable]
public struct GameEvent
{
	internal string Name { get => _name; }
	internal DateTime Time { get => _timestamp; set => _timestamp = value; }
	internal Value[] Params { get => _parameters; }
	internal int ParamsCount { get => _currentCount; }
	internal bool HasValidTimestamps { get => _hasValidTimestamps; set => _hasValidTimestamps = value; }
	
	private int _currentCount;

	private string 		_name;
	internal DateTime 	_timestamp;
	internal Value[] 	_parameters;
	private bool _hasValidTimestamps;

	public void Add(string name, int value)
	{
		if (_currentCount == _parameters.Length)
			ExtendParameterPool();
		_parameters[_currentCount++].Set(name, value);
	}

	public void Add(string name, double value)
	{
		if (_currentCount == _parameters.Length)
			ExtendParameterPool();
		_parameters[_currentCount++].Set(name, value);
	}

	public void Add(string name, bool value)
	{
		if (_currentCount == _parameters.Length)
			ExtendParameterPool();
		_parameters[_currentCount++].Set(name, value);
	}

	public void Add(string name, DateTime value)
	{
		if (_currentCount == _parameters.Length)
			ExtendParameterPool();
		_parameters[_currentCount++].Set(name, value);
	}
	
	public void Add(string name, string value)
	{
		if (_currentCount == _parameters.Length)
			ExtendParameterPool();
		_parameters[_currentCount++].Set(name, value);
	}
	
	internal void Add(in Value v)
	{
		if (_currentCount == _parameters.Length)
			ExtendParameterPool();
		_parameters[_currentCount++].Set(in v);
	}
	
	internal void Add(string name, string value, Value.EValueType type)
	{
		if (_currentCount == _parameters.Length)
			ExtendParameterPool();
		_parameters[_currentCount++].Set(name, value, type);
	}

	public void ToJson(long id, StringBuilder sb)
	{
		sb.Append($"{{\"user_id\":{id}, \"name\":\"{_name}\", \"event_time\":\"{_timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture)}\", \"current_app_version\":\"{Application.version}\", \"parameters\":");
		ParametersToJson(sb);
		sb.Append('}');
	}
	
	internal void Free()
	{
		_currentCount = 0;
	}
	
	internal void SetName(string name)
	{
		_name = name;
	}
	
	internal void SetMaxParameterCount(int count)
	{
		Array.Resize(ref _parameters, count);
	}
		
	internal void SetTimestamp(DateTime timestamp)
	{
		//Debug.LogWarning($"[ADVANT] event time = {timestamp}");
		_timestamp = timestamp; //.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture);
	}
		
	private void ExtendParameterPool()
	{
		try
		{
			Array.Resize(ref _parameters, _parameters.Length * 2);
		}
		catch (Exception e)
		{
			Debug.LogError($"Cannot add more parameters to the event (new count = {_parameters.Length * 2}): {e.Message}");
		}
	}
	
	private void ParametersToJson(StringBuilder sb)
	{
		sb.Append('[');
		if (_parameters != null && _currentCount != 0)
		{
			for (int i = 0; i < _currentCount; ++i)
			{
				if (i > 0)
					sb.Append(',');
				_parameters[i].ToJson(sb);
			}
		}
		sb.Append(']');
	}
}			
}