using System;
using System.Globalization;

internal struct Session
{
	private long _sessionCount;
	private int _area;
	
	private DateTime _lastActivity;
	
	internal void 		SetSessionCount(long sessionCount)	=> _sessionCount = sessionCount;
	internal long 		GetSessionCount()					=> _sessionCount; 		
	
	internal void 		SetArea(int area) 					=> _area = area;
	
	internal void 		SetLastActivity(DateTime dateTime)	=> _lastActivity = dateTime;
	internal DateTime	GetLastActivity() 					=> _lastActivity;
	
	internal string ToJson(long userId)
	{
		return $"{{\"UserId\":{userId}, \"Area\":\"{_area}\", \"SessionStarts\":\"{DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture)}\",\"LastActivity\":\"{_lastActivity.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture)}\", \"SessionCount\": {_sessionCount}}}";
	}
	
	// internal void MarkAsRegistered()
	// {
		// _isRegistered = true;
	// }
}