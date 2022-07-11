﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Advant.Data
{
    internal interface IGameData
    {
        string Name { get; }
        void ToJson(long id, StringBuilder sb);
    }

    [Serializable]
    internal class Cache<T> where T : IGameData
    {
        List<T> _data = new List<T>();
        StringBuilder _sb = new StringBuilder();

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
            _sb.Clear();
            return result;
        }

        public void Clear()
        {
            _data.Clear();
        }
    }
}
