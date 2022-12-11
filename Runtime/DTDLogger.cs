using System;

namespace Advant
{

internal class DTDLogger
{
	private delegate void _failureDelegate(string failure, Exception exception, Type advInnerType = null);
	private delegate void _messageDelegate(string message);
	private delegate void _webRequestDelegate(string requestName, 
											  bool isSuccess,
											  int statusCode,
											  string requestError,
											  string exception);
	private delegate void _dataSendingDelegate(string dataType,
											   int batchSize,
											   bool isSuccess,
											   int statusCode,
											   string requestError,
											   string exception);

	public void InitDelegates(Action<string> messageLogger, 
							  Action<string, Exception, Type> failureLogger, 
							  Action<string, bool, int, string, string> webRequestLogger,
							  Action<string, int, bool, int, string, string> dataSendingLogger)
	{
			_messageDelegate = messageLogger;
			_failureDelegate = failureLogger;
			_webRequestDelegate = webRequestLogger;
			_dataSendingDelegate = dataSendingLogger;
	}
														
	public void LogMessage(string message)
	{
		if (_messageDelegate != null)
			_messageDelegate(message);
	}
	
	public void LogFailure(string failure, Exception exception, Type advInnerType = null)
	{
		if (_failureDelegate != null)
			_failureDelegate(failure, exception, advInnerType);
	}
	
	public void LogWebRequest(string requestName, 
							  bool isSuccess,
							  int statusCode,
							  string requestError,
							  string exception)
	{
		if (_webRequestDelegate != null)
			_webRequestDelegate(requestName, isSuccess, statusCode, requestError, exception);
	}
	
	public void LogDataSending(string dataType,
							   int batchSize,
							   bool isSuccess,
							   int statusCode,
							   string requestError,
							   string exception)
	{
		if (_dataSendingDelegate != null)
			_dataSendingDelegate(dataType, batchSize, isSuccess, statusCode, requestError, exception);
	}
							
}

}