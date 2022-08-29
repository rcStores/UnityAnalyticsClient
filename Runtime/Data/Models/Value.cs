using System;
using System.Globalization;
using System.Text;

namespace Advant.Data.Models
{
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
}