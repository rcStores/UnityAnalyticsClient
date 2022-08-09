using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Advant.Data.Models
{
    [Serializable]
    internal class GameProperty : IGameData
    {
        private GameProperty(string table, string name, string value, EValueType type) 
        {
			_table = table;
            _name = name;
            _value = value;
            _type = type;
        }

        public static GameProperty Create<T>(string tableName, string name, T value)
        {
			Debug.Log($"Create property; name = {name}, value = {value}");
            return new GameProperty(tableName, name, value?.ToString(), NativeTypesDescription[value.GetType()]);
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

        public void ToJson(long id, StringBuilder sb)
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
				
            sb.Append($"{{\"table\":\"{_table}\", \"user_id\":{id}, \"name\":\"{_name}\", \"value\":{valueStr}, \"type\":{(int)_type}}}");
            Debug.Log("Property in JSON: " + sb);
        }

        public string Name => _name;
		public string Table => _table;

		private string _table;
        private string _name;
        private string _value;
        private EValueType _type;
    }
}
