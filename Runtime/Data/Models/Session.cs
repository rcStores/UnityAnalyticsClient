using System;
using System.Text;
using System.Globalization;

[Serializable]
internal struct Session
{
	public DateTime SessionStart 		{ get; set; }
	public DateTime	LastActivity 		{ get; set; }
	public DateTime	LastValidTimestamp	{ get; set; }
	public int 		Area 				{ get; set; }
	public long 	SessionCount		{ get; set; }
	public string 	AbMode 				{ get; set; }
	public bool 	Unregistered 		{ get; set; }
	public string 	GameVersion 		{ get; set; }
	
	internal bool HasValidTimestamps { get => _hasValidTimestamps; set => _hasValidTimestamps = value; }
	
	private bool _hasValidTimestamps;
	//public string 	SessionId 		{ get; set; }
	
	internal void ToJson(long userId, StringBuilder sb)
	{
		sb.Append($"{{\"UserId\":{userId}, \"Area\":\"{Area}\"," 
			+ $"\"SessionStarts\":\"{SessionStart.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture)}\"," 
			+ $"\"LastActivity\":\"{LastActivity.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture)}\"," 
			+ $"\"AbMode\":\"{AbMode}\", \"SessionCount\": {SessionCount}, \"IsUnregistered\": {Unregistered.ToString().ToLower()},"
			+ $"\"GameVersion\":\"{GameVersion}\"}}");
	}
}