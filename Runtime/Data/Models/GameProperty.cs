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
        private GameProperty(string table, ParameterValue value) 
        {
			_table = table;
			_value = value;
        }

        public static GameProperty Create<T>(string tableName, string name, T value)
        {
			//Debug.Log($"Create property; name = {name}, value = {value}");
            return new GameProperty(tableName, ParameterValue.Create(name, value));
        }

        public void ToJson(long id, StringBuilder sb)
        {
            sb.Append($"{{\"table\":\"{_table}\", \"user_id\":{id}, \"value\":");
			_value.ToJson(sb);
			sb.Append('}');
            //Debug.Log("Property in JSON: " + sb);
        }

        public string Name => _value.Name;
		public string Table => _table;

		private string _table;
		private ParameterValue _value;
    }
}
