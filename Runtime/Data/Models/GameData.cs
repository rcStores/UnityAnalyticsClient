using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Advant.Data.Models
{
	internal interface IGameData
    {
        string Name { get; }
		string Table { get; }
        void ToJson(long id, StringBuilder sb);
    }
	
	internal public class ParameterValue
	{	
		internal enum EValueType
		{
			Int, Float, Double, String, Bool, DateTime
		}
		
		static readonly Dictionary<Type, EValueType> NativeTypesDescription = new Dictionary<Type, EValueType>()
            {
                { typeof(int), EValueType.Int },
                { typeof(string), EValueType.String },
                { typeof(float), EValueType.Float },
                { typeof(double), EValueType.Double },
                { typeof(bool), EValueType.Bool },
                { typeof(DateTime), EValueType.DateTime },
            };
			
		public string Name { get => _name; }
		
		private string _name;
		private string _value;
		private EValueType _type;
		
		private ParameterValue(string name, string value, EValueType type)
		{
			_name = name;
			_value = value;
			_type = type;
		}
		
		public void ToJson(StringBuilder sb)
        {
            string valueStr; 
			if (_type == EValueType.DateTime)
			{
				valueStr = $"\"{DateTime.Parse(_value).ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture)}\"";
			}
			else if (_value is null)
			{
				valueStr = "null";
			}
			else
			{
				valueStr = $"\"{_value.ToString()}\"";
			}
				
            sb.Append($"{{\"name\":\"{_name}\", \"value\":{valueStr}, \"type\":{(int)_type}}}");
            //Debug.Log("Property in JSON: " + sb);
        }
		
		public static ParameterValue<T> Create(string name, T value)
		{
			return new ParameterValue(name, value?.ToString(), NativeTypesDescription[value.GetType()]);
		}
	}
}
