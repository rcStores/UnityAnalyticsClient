using System;
using System.Text;
using System.Globalization;

[Serializable]
internal struct Session
{
	public DateTime SessionStart 	{ get; set; }
	public DateTime	LastActivity 	{ get; set; }
	public int 		Area 			{ get; set; }
	public long 	SessionCount	{ get; set; }
	public string 	AbMode 			{ get; set; }
	public string 	SessionId 		{ get; set; }
	
	internal void ToJson(long userId, StringBuilder sb)
	{
		sb.Append($"{{\"UserId\":{userId}, \"Area\":\"{Area}\", \"SessionStarts\":\"{SessionStart.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture)}\"," 
			+ $"\"LastActivity\":\"{LastActivity.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture)}\", \"SessionId\": \"{SessionId}\", \"AbMode\":\"{AbMode}\"}}");
	}
}