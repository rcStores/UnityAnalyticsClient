using System;
using System.Text;

namespace Advant.Data.Models
{
	
[Serializable]
internal struct GameProperty
{
	private string 	_table;
	private Value 	_value;
	
	public void Set(string name, int value) 		=> _value.Set(name, value);
	public void Set(string name, double value) 		=> _value.Set(name, value);
	public void Set(string name, string value) 		=> _value.Set(name, value);
	public void Set(string name, bool value) 		=> _value.Set(name, value);
	internal void Set(string name, DateTime value) 	=> _value.Set(name, value);
	
	public void SetTableName(string table) 			=> _table = table;	
		
	public void ToJson(long id, StringBuilder sb)
	{
		sb.Append($"{{\"table\":\"{_table}\", \"user_id\":{id}, \"value\":");
		_value.ToJson(sb);
		sb.Append('}');
	}
}
}