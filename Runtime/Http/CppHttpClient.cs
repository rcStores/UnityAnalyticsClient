using Cysharp.Threading.Tasks;
using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using UnityEngine;
using System.Runtime.InteropServices;

using Advant;
using Advant.Data.Models;
using Advant.Data;

namespace Advant.Http
{
	
public struct InteropResponse
{
	long statusCode;
	double elapsedTime;
	long redirectCount;

	StringBuilder errorMessage;
	int errorLength;
	int actualErrorLength;

	StringBuilder reasonPhrase;
	int reasonLength;
	int actualReasonLength;
	
	StringBuilder body;
	int bodyLength;
	int actualBodyLength;
}
	
internal class CppHttpClient : IHttpClient
{
	private readonly Dictionary<Type, string> _gameDataEndpointsByType = new();
	
	private string _getTesterEndpoint;
	private string _getNetworkTimeEndpoint;
	private string _getCountryEndpoint;
	private string _putUserIdEndpoint;
	private string _putSessionCountEndpoint;
	
	private IntPtr _core;

    public CppHttpClient()
    {
		 _core = createCore();
	}
	
	#region Import
	
	[DllImport("WebRequestsCore.dll")]
    private static extern IntPtr CreateCore();
	
	[DllImport("WebRequestsCore.dll")]
    private static extern IntPtr FreeCore(IntPtr instance);
    
    [DllImport("WebRequestsCore.dll")]
    private static extern IntPtr Post(IntPtr instance, string endpoint, string body);
	
    [DllImport("WebRequestsCore.dll")]
    private static extern IntPtr Put(IntPtr instance, string endpoint, string body);
    
    [DllImport("WebRequestsCore.dll")]
    private static extern IntPtr Get(IntPtr instance, string endpoint);
	
	[DllImport("WebRequestsCore.dll")]
    private static extern void WriteResponse(IntPtr response, ref InteropResponse output);
	
	#endregion

	#region Init

	public void SetPathBases(string analytics, string registration)
	{
		_getTesterEndpoint = registration + "/Registration/GetTester";
		_getNetworkTimeEndpoint = registration + "/Registration/GetNetworkTime";
		_getCountryEndpoint = "http://ip-api.com/json/"; //"https://ipapi.co/country/";
		_putUserIdEndpoint = registration + "/Registration/GetOrCreateUserId2";
		_putSessionCountEndpoint = registration + "/Sessions/PutSessionCount";
		_gameDataEndpointsByType[typeof(GameProperty)] = analytics + "/AnalyticsData/SendProperties";
		_gameDataEndpointsByType[typeof(GameEvent)] = analytics + "/AnalyticsData/SendEvents";
		_gameDataEndpointsByType[typeof(Session)] = registration + "/Sessions/SaveSession";
	}

	#endregion
	
	#region Sending utils
	
	private void PrepareDataSendingResult(ref DataSendingResult result, InteropResponse parsedOutput)
	{
		result.Age = parsedOutput.elapsedTime.ToString();
		result.IsSuccess = parsedOutput.statusCode == 200 || parsedOutput.statusCode == 201;
		result.StatusCode = (int)parsedOutput.statusCode;
			
		if (parsedOutput.actualErrorLength > parsedOutput.errorLength)
			result.ExceptionMesssage = "Couldn't show the error message " +
				"because it's longer than allocated capacity";
		else		
			result.ExceptionMesssage = parsedOutput.error.ToString(0, parsedOutput.actualErrorLength);
			
		if (parsedOutput.actualReasonLength > parsedOutput.reasonLength)
			result.RequestError = "Couldn't show the reason phrase " +
				"because it's longer than allocated capacity";
		else		
			result.RequestError = parsedOutput.reasonPhrase.ToString(0, parsedOutput.actualReasonLength);
	}
	
	private string GetBody(InteropResponse response)
	{
		return response.actualReasonLength > parsedOutput.reasonLength ?
			null :
			response.body.ToString(0, response.actualBodyLength);
	}
	
	private InteropResponse CreateInteropResponseModel()
	{
		return new InteropResponse
		{
			errorMessage = new StringBuilder(1000),
			errorLength = errorMessage.Capacity,
			reasonPhrase = new StringBuilder(1000),
			reasonLength = reasonPhrase.Capacity,
			body = new StringBuilder(1000),
			bodyLength = body.Capacity,
		};
	}
	
	#endregion

	public async UniTask<DataSendingResult> SendToServerAsync<TGameData>(string json)
	{
		var result = new DataSendingResult();
		if (string.IsNullOrEmpty(json))
		{
			result.IsSuccess = true;
			result.RequestError = "Empty content";
			return result;
		}
		
		try
		{
			var output = CreateInteropResponseModel();
			IntPtr response = await Task.Run(() => Post(_core, _gameDataEndpointsByType[typeof(TGameData)], json));
			WriteResponse(response, ref output);
			PrepareDataSendingResult(ref result, output);
			Debug.Log($"SendToServerAsync: {result.StatusCode}-{result.RequestError}");
		}
		catch (Exception e)
		{
			result.IsSuccess = false;
			result.ExceptionMessage = $"Message: {ioe.Message}\nInner exception message: {ioe.InnerException?.Message}";
			
			Debug.LogWarning($"SendToServerAsync: {result.ExceptionMessage}");

			AdvAnalytics.LogFailureToDTD("send_to_server_failure", ioe, typeof(TGameData));
		}
		return result;
	}

	public async UniTask<(bool, DateTime)> GetNetworkTime(CancellationToken token, int timeout = 0)
	{
		try
		{
			var (isCancelled, response) = await Task.Run(() => Get(_core, _getNetworkTimeEndpoint), token)
				.AsUniTask()
				.SuppressCancellationThrow();
			var result = new DataSendingResult();
			var output = CreateInteropResponseModel();
			WriteResponse(response, ref output);
			PrepareDataSendingResult(ref result, output);
			
			if (isCancelled)
				return (true, default);
			
			Debug.LogWarning($"GetNetworkTime: {output.statusCode}-{output.reasonPhrase}");
			
			if (output.statusCode != 200 && output.statusCode != 201)
				throw new Exception($"Error status code while getting network time: {output.statusCode}");
			
			Advant.AdvAnalytics.LogWebRequestToDTD("get_network_time",
													result.IsSuccess,
													(int)result.StatusCode,
													result.RequestError,
													result.RequestMessage);
			
			DateTime.TryParseExact(GetBody(response),
								   "yyyy-MM-ddTHH:mm:ss.fff",
								   CultureInfo.InvariantCulture,
								   DateTimeStyles.None,
								   out DateTime result);
			return (false, result);
		}
		catch (Exception e)
		{
			Advant.AdvAnalytics.LogFailureToDTD("get_time_failure", e);
			Debug.LogWarning($"GetNetworkTime: {e.Message}");
			return (false, default);
		}
	}

	public async UniTask<bool> GetTester(long userId)
	{
		try
		{
			var response = await Task.Run(() => Get(_getTesterEndpoint + $"/{userId}"));
			
			var result = new DataSendingResult();
			var output = CreateInteropResponseModel();
			WriteResponse(response, ref output);
			PrepareDataSendingResult(ref result, output);
			
			if (output.statusCode != 200 && output.statusCode != 201)
				throw new Exception($"Error status code while getting network time: {output.statusCode}");
			
			Advant.AdvAnalytics.LogWebRequestToDTD("get_tester",
													result.IsSuccess,
													(int)result.StatusCode,
													result.RequestError,
													result.RequestMessage);
			Debug.Log($"GetNetworkTime: {result.StatusCode}-{result.RequestError}");
			return Convert.ToBoolean(GetBody(response));
		}
		catch (Exception e)
		{
			Debug.LogWarning($"GetTester: {e.Message}");
			Advant.AdvAnalytics.LogFailureToDTD("get_tester_unexpected_failure", e);
			return false;
		}
	}

	public async UniTask<string> GetCountryAsync(int timeout)
	{
		try
		{
			var response = await Task.Run(() => Get(_getCountryEndpoint));
			
			var result = new DataSendingResult();
			var output = CreateInteropResponseModel();
			WriteResponse(response, ref output);
			PrepareDataSendingResult(ref result, output);
			
			if (output.statusCode != 200 && output.statusCode != 201)
				throw new Exception($"Error status code while getting network time: {output.statusCode}");
			
			Advant.AdvAnalytics.LogWebRequestToDTD("get_country",
													result.IsSuccess,
													(int)result.StatusCode,
													result.RequestError,
													result.RequestMessage);
													
			Debug.Log($"GetCountryAsync: {result.StatusCode}-{result.RequestError}");
			
			var jsonNode = JSONNode.Parse(GetBody(response));
			return jsonNode["country"];
		}
		catch (Exception e)
		{
			Debug.Log($"GetCountry: {e.Message}");
			Advant.AdvAnalytics.LogFailureToDTD("get_country_unexpected_failure", e);
		}
		return null;
	}

	public async UniTask<UserIdResponse> GetOrCreateUserIdAsync(RegistrationToken dto)
	{
		var userIdResponse = new UserIdResponse();
		try
		{
			var response = await Task.Run(() => Put(_putUserIdEndpoint, dto.ToJson()));
			
			var result = new DataSendingResult();
			var output = CreateInteropResponseModel();
			WriteResponse(response, ref output);
			PrepareDataSendingResult(ref result, output);
			
			Advant.AdvAnalytics.LogWebRequestToDTD("get_user_id",
												  result.IsSuccess,
											      (int) result.StatusCode,
												  result.RequestError,
												  result.RequestMessage);

			if (result.IsSuccess)
			{
				var jsonNode = JSONNode.Parse(GetBody(response));
				userIdResponse.UserId = jsonNode["userId"];
				userIdResponse.SessionCount = jsonNode["sessionCount"];
			}
			else
			{
				userIdResponse.UserId = -1;
			}
			Debug.Log($"GetOrCreateUserId: {result.StatusCode}-{result.RequestError}");

			//Debug.LogWarning($"[ADVANAL] GetOrCreateUserIdAsync. UserId = {result.UserId}, SessionCount = {result.SessionCount}");
		}
		catch (Exception e)
		{
			AdvAnalytics.LogFailureToDTD("get_user_id", e);
			Debug.LogWarning($"GetOrCreateUserId: {e.Message}");
			userIdResponse.UserId = -1;
		}
		return userIdResponse;
	}

	public async UniTask<bool> PutSessionCount(long userId, long sessionCount)
    {
		var result = false;
		try
		{
			var response = await Task.Run(() => Put(
				_putSessionCountEndpoint, 
				$"{{\"UserId\":{userId},\"SessionCount\":{sessionCount}}}"));
			
			var result = new DataSendingResult();
			var output = CreateInteropResponseModel();
			WriteResponse(response, ref output);
			PrepareDataSendingResult(ref result, output);
			
			AdvAnalytics.LogWebRequestToDTD("put_session_count",
											result.IsSuccess,
											(int) result.StatusCode,
											result.RequestError,
											result.RequestMessage);
			result = Convert.ToBoolean(GetBody(response));
		}
		catch (Exception e)
		{
			AdvAnalytics.LogFailureToDTD("put_session_count", e);
		}
		return result;
	}
}
}