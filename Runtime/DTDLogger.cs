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

	public void InitDelegates(Action<string> messageLogger, 
							  Action<string, Exception, Type> failureLogger, 
							  Action<string, bool, int, string, string> webRequestLogger,
							  Action<string, int, bool, int, string, string> dataSendingLogger)
	{
			MessageDelegate = new MessageDelegate(messageLogger);
			FailureDelegate = new FailureDelegate(failureLogger);
			WebRequestDelegate = new WebRequestDelegate(webRequestLogger);
			DataSendingDelegate = new DataSendingDelegate(dataSendingLogger);
	}
														
	public void LogMessage(string message)
	{
		if (MessageDelegate != null)
			MessageDelegate(message);
	}
	
	public void LogFailure(string failure, Exception exception, Type advInnerType = null)
	{
		if (FailureDelegate != null)
			FailureDelegate(failure, exception, advInnerType);
	}
	
	public void LogWebRequest(string requestName, 
							  bool isSuccess,
							  int statusCode,
							  string requestError,
							  string exception)
	{
		if (WebRequestDelegate != null)
			WebRequestDelegate(requestName, isSuccess, statusCode, requestError, exception);
	}
	
	public void LogDataSending(string dataType,
							   int batchSize,
							   bool isSuccess,
							   int statusCode,
							   string requestError,
							   string exception)
	{
		if (DataSendingDelegate != null)
			DataSendingDelegate(dataType, batchSize, isSuccess, statusCode, requestError, exception);
	}
							
}

}