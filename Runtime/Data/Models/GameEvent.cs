using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Advant.Data.Models 
{
    [Serializable]
    internal class GameEvent : IGameData
    {
        public GameEvent(string name, DateTime eventTime, string currentAppVersion, Dictionary<string, object> parameters)
        {
            this.name = name;
            event_time = eventTime;
            current_app_version = currentAppVersion;
            this.parameters = parameters;
        }

        public void ToJson(long id, StringBuilder sb)
        {
            sb.Append($"{{\"user_id\":{id}, \"name\":\"{name}\", \"event_time\":\"{event_time.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture)}\", \"current_app_version\":\"{current_app_version}\", \"parameters\":"); 
            parameters.ToJson(sb);
            sb.Append('}');
        }

        public string Name => name;

        public string name;
        public DateTime event_time;
        public string current_app_version;
        public Dictionary<string, object> parameters;
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
                        valueStr = $"\"{item.Value}\"";
                    }
                    else if (item.Value is DateTime dt)
                    {
                        valueStr = $"\"{((DateTime)item.Value).ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture)}\"";
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
        }
    }
}
