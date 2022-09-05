using System;
using System.Text;
using System.Globalization;

[Serializable]
internal struct Session
{
	private long _sessionCount;
	private int _area;
	
	private DateTime _sessionStart;
	private DateTime _lastActivity;
	
	internal void 		SetSessionCount(long sessionCount)	=> _sessionCount = sessionCount;
	internal long 		GetSessionCount()					=> _sessionCount; 		
	
	internal void 		SetArea(int area) 					=> _area = area;
	internal int 		GetArea() 							=> _area;
	
	internal void 		SetLastActivity(DateTime dateTime)	=> _lastActivity = dateTime;
	internal DateTime	GetLastActivity() 					=> _lastActivity;
	
	internal void 		SetSessionStart(DateTime dateTime)	=> _sessionStart = dateTime;
	
	internal string ToJson(long userId, StringBuilder sb)
	{
		sb.Append($"{{\"UserId\":{userId}, \"Area\":\"{_area}\", \"SessionStarts\":\"{DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture)}\",\"LastActivity\":\"{_lastActivity.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture)}\", \"SessionCount\": {_sessionCount}}}");
	}
	
	// internal void MarkAsRegistered()
	// {
		// _isRegistered = true;
	// }
}