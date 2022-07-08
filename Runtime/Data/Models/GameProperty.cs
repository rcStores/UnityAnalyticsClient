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
        private GameProperty(string name, string value, EValueType type) 
        {
            user_id = -1;
            this.name = name;
            this.value = value;
            this.type = type;
        }

        public static GameProperty Create<T>(string name, T value)
        {
			Debug.Log($"Create property; name = {name}, value = {value}");
            return new GameProperty(name, value.ToString(), NativeTypesDescription[value.GetType()]);
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
            string valueStr = type == EValueType.DateTime ? value.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture) : value.ToString();
            sb.Append($"{{\"user_id\":{id}, \"name\":\"{name}\", \"value\":\"{valueStr}\", \"type\":{(int)type}}}");
            Debug.Log("Property in JSON: " + sb);
        }

        public string Name => name;

        public long user_id;
        public string name;
        public string value;
        public EValueType type;
    }
}
