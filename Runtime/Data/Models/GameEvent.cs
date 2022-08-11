using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Advant.Data.Models 
{
    [Serializable]
    internal class GameEvent : IGameData
    {
        public GameEvent(string name, DateTime eventTime, string currentAppVersion, Dictionary<string, object> parameters)
        {
            _name = name;
            _event_time = eventTime;
            _current_app_version = currentAppVersion;
            _parameters = parameters;
        }

        public void ToJson(long id, StringBuilder sb)
        {
            sb.Append($"{{\"user_id\":{id}, \"name\":\"{_name}\", \"event_time\":\"{_event_time.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture)}\", \"current_app_version\":\"{_current_app_version}\", \"parameters\":"); 
            _parameters.ToJson(sb);
            sb.Append('}');
        }

        public string Name => _name;
		public string Table => "_" + _name;

        private string _name;
        private DateTime _event_time;
        private string _current_app_version;
        public Dictionary<string, object> _parameters;
    }

    // ECS unity
    internal static class ParametersDictionaryExtensions
    {
        public static void ToJson(this Dictionary<string, object> dict, StringBuilder sb)
        {
            sb.Append('{');
            if (dict != null)
            {
                foreach (var item in dict)
                {
                    string valueStr;
                    if (item.Value is string str) 
                    {
                        valueStr = $"\"{((string)item.Value).Replace("\n", @"\\n")}\"";
                    }
                    else if (item.Value is DateTime dt)
                    {
                        valueStr = $"\"{((DateTime)item.Value).ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture)}\"";
                    }
					else if (item.Value is bool b)
					{
						valueStr = item.Value.ToString().ToLower();
					}
                    else
                    { 
                        valueStr = item.Value.ToString();
                    }

                    sb.Append($"\"{item.Key}\":{valueStr},");
                }
                sb.Remove(sb.Length - 1, 1);
            }
            sb.Append('}');
			//Debug.LogWarning("GameEvent JSON:\n" + sb);
        }
    }
}
