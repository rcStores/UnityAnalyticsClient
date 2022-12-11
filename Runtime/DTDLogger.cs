using System;

namespace Advant
{

internal class DTDLogger
{
	private delegate void FailureDelegate(string failure, Exception exception, Type advInnerType = null);
	private delegate void MessageDelegate(string message);
	private delegate void WebRequestDelegate(string requestName, 
											  bool isSuccess,
											  int statusCode,
											  string requestError,
											  string exception);
	private delegate void DataSendingDelegate(string dataType,
											   int batchSize,
											   bool isSuccess,
											   int statusCode,
											   string requestError,
											   string exception);
											   
	private FailureDelegate _logFailure;
	private MessageDelegate _logMessage;
	private WebRequestDelegate _logWebRequest;
	private DataSendingDelegate _logDataSending;

	public void InitDelegates(Action<string> messageLogger, 
							  Action<string, Exception, Type> failureLogger, 
							  Action<string, bool, int, string, string> webRequestLogger,
							  Action<string, int, bool, int, string, string> dataSendingLogger)
	{
			_logMessagee = new MessageDelegate(messageLogger);
			_logFailure = new FailureDelegate(failureLogger);
			_logWebRequest = new WebRequestDelegate(webRequestLogger);
			_logDataSending = new DataSendingDelegate(dataSendingLogger);
	}
														
	public void LogMessage(string message)
	{
		if (_logMessage != null)
			_logMessage(message);
	}
	
	public void LogFailure(string failure, Exception exception, Type advInnerType = null)
	{
		if (_logFailure != null)
			_logFailure(failure, exception, advInnerType);
	}
	
	public void LogWebRequest(string requestName, 
							  bool isSuccess,
							  int statusCode,
							  string requestError,
							  string exception)
	{
		if (_logWebRequest != null)
			_logWebRequest(requestName, isSuccess, statusCode, requestError, exception);
	}
	
	public void LogDataSending(string dataType,
							   int batchSize,
							   bool isSuccess,
							   int statusCode,
							   string requestError,
							   string exception)
	{
		if (_logDataSending != null)
			_logDataSending(dataType, batchSize, isSuccess, statusCode, requestError, exception);
	}
							
}

}