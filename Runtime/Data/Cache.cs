using Advant.Data.Models;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Advant.Data
{
    internal interface IGameData
    {
        string Name { get; }
		string Table { get; }
        void ToJson(long id, StringBuilder sb);
    }

    [Serializable]
    internal class Cache<T> where T : IGameData
    {
		public Cache()
		{
			_data = new List<T>();
			_sb = new StringBuilder();
		}
		
        public Cache(IList<T> data)
        {
            if (typeof(T) == typeof(GameEvent))
            {
                _data = new List<T>(data);
            }
            else
            {
                _data = new List<T>
                {
                    Capacity = data.Count
                };
                foreach (var elem in data)
                {
                    AddUnique(elem);
                }
            }
            _sb = new StringBuilder();
        }

        List<T> _data;
        StringBuilder _sb;
		
		public int Count { get => _data.Count; }

        public IList<T> Get()
        {
            return _data;
        }

        public bool IsEmpty()
        {
            return _data.Count == 0;
        }

        public void Add(T element)
        {
            _data.Add(element);
        }

        public void AddUnique(T newElement)
        {
            for (int i = 0; i < _data.Count; ++i)
            {
                if (_data[i].Name == newElement.Name && _data[i].Table == newElement.Table)
                {
                    _data[i] = newElement;
                    return;
                }
            }
            _data.Add(newElement);
        }
        
        public string ToJson(long id)
        {
            _sb.Append('[');
            foreach (var elem in _data)
            {
                elem.ToJson(id, _sb);
                _sb.Append(',');
                
            }
            string result = _sb.Replace(',', ']', _sb.Length - 1, 1).ToString();
			// Debug.LogWarning("Resulting cache:\n");
			// foreach (var word in result.Split(','))
			// {
				// Debug.LogWarning(word);
			// }
            _sb.Clear();
            return result;
        }

        public void Clear()
        {
            _data.Clear();
        }
    }
}
