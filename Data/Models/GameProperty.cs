﻿using System;
using System.Collections.Generic;
using System.Text;

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
            sb.Append($"{{\"user_id\":{id}, \"name\":\"{name}\", \"value\":\"{value}\", \"type\":{(int)type}}}");
        }

        public string Name => name;

        public long user_id;
        public string name;
        public string value;
        public EValueType type;
    }
}
