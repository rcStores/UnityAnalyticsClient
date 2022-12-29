using Cysharp.Threading.Tasks;
using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Threading;
using UnityEngine;

using Advant;
using Advant.Data.Models;
using Advant.Data;

namespace Advant.Http
{
	
internal interface IHttpClient
{
	public void SetPathBases(string analytics, string registration);
	public UniTask<DataSendingResult> SendToServerAsync<TGameData>(string json);
	public UniTask<(bool, DateTime)> GetNetworkTime(CancellationToken token, int timeout = 0);
	public UniTask<bool> GetTester(long userId);
	public UniTask<string> GetCountryAsync(int timeout);
	public UniTask<UserIdResponse> GetOrCreateUserIdAsync(RegistrationToken dto);
	public UniTask<bool> PutSessionCount(long userId, long sessionCount);
}	
	
internal class AdvantHttpClient : HttpClient, IHttpClient
{
	private readonly Dictionary<Type, string> _gameDataEndpointsByType = new();

	private string _getTesterEndpoint;
	private string _getNetworkTimeEndpoint;
	private string _getCountryEndpoint;
	private string _putUserIdEndpoint;
	private string _putSessionCountEndpoint;

    // todo: client configs in ctor
	// todo: add unconditional logging of each web request
    public AdvantHttpClient()
    {
		Timeout = System.Threading.Timeout.InfiniteTimeSpan;
	}

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
			var response = await PostAsync(
				_gameDataEndpointsByType[typeof(TGameData)], new StringContent(json, Encoding.UTF8, "application/json"));

			result.Age = response.Headers.Age.GetValueOrDefault().ToString();
			result.IsSuccess = response.IsSuccessStatusCode;
			result.StatusCode = (int)response.StatusCode;
			result.RequestError = response.ReasonPhrase;
			
			//Debug.Log($"SendToServerAsync: {result.StatusCode}-{result.RequestError}");
		}
		catch (HttpRequestException hre)
		{
			result.IsSuccess = false;
			result.RequestError = "Connection/DNS/certificate/timeout issue";
			result.ExceptionMessage = $"Message: {hre.Message}\nInner exception message: {hre.InnerException?.Message}";
			
			//Debug.Log($"SendToServerAsync: {result.StatusCode}-{result.RequestError}\n{result.ExceptionMessage}");
			
			AdvAnalytics.LogFailureToDTD("send_to_server_failure", hre, typeof(TGameData));
		}
		catch (UriFormatException ufe)
		{
			result.IsSuccess = false;
			result.RequestError = $"{_gameDataEndpointsByType[typeof(TGameData)]} is not correct absolute or relative URI";
			result.ExceptionMessage = $"Message: {ufe.Message}\nInner exception message: {ufe.InnerException?.Message}";
			
			//Debug.Log($"SendToServerAsync: {result.StatusCode}-{result.RequestError}\n{result.ExceptionMessage}");

			AdvAnalytics.LogFailureToDTD("send_to_server_failure", ufe, typeof(TGameData));
		}
		catch (InvalidOperationException ioe)
		{
			result.IsSuccess = false;
			result.RequestError = "Request URI must be relative otherwise BaseAddress must be set";
			result.ExceptionMessage = $"Message: {ioe.Message}\nInner exception message: {ioe.InnerException?.Message}";
			
			//Debug.Log($"SendToServerAsync: {result.StatusCode}-{result.RequestError}\n{result.ExceptionMessage}");

			AdvAnalytics.LogFailureToDTD("send_to_server_failure", ioe, typeof(TGameData));
		}
		return result;
	}

	public async UniTask<(bool, DateTime)> GetNetworkTime(CancellationToken token, int timeout = 0)
	{
		try
		{
			Timeout = timeout == 0 ?
				Timeout :
				TimeSpan.FromSeconds(timeout);
				
			var (isCancelled, response) = await (GetAsync(_getNetworkTimeEndpoint, HttpCompletionOption.ResponseContentRead, token))
				.AsUniTask()
				.SuppressCancellationThrow();

			if (isCancelled)
				return (true, default);
			response.EnsureSuccessStatusCode();
			//Debug.Log($"GetNetworkTime: {response.StatusCode}-{response.ReasonPhrase}");
			Advant.AdvAnalytics.LogWebRequestToDTD("get_network_time",
													response.IsSuccessStatusCode,
													(int)response.StatusCode,
													response.ReasonPhrase,
													exception: null);

			DateTime.TryParseExact(await response.Content.ReadAsStringAsync(),
								   "yyyy-MM-ddTHH:mm:ss.fff",
								   CultureInfo.InvariantCulture,
								   DateTimeStyles.None,
								   out DateTime result);
			return (false, result);
		}
		catch (HttpRequestException hre)
		{
			Advant.AdvAnalytics.LogFailureToDTD("get_time_failure", hre);
			//Debug.Log($"GetNetworkTime: {hre.Message}");
			return (false, default);
		}
		catch (Exception e)
		{
			Advant.AdvAnalytics.LogFailureToDTD("get_time_unexpected_failure", e);
			//Debug.Log($"GetNetworkTime: {e.Message}");
			return (false, default);
		}
		finally
		{
			Timeout = System.Threading.Timeout.InfiniteTimeSpan;
		}
	}

	public async UniTask<bool> GetTester(long userId)
	{
		try
		{
			var response = await GetAsync(_getTesterEndpoint + $"/{userId}", HttpCompletionOption.ResponseContentRead);
			response.EnsureSuccessStatusCode();
			Advant.AdvAnalytics.LogWebRequestToDTD("get_tester",
													response.IsSuccessStatusCode,
													(int)response.StatusCode,
													response.ReasonPhrase,
													exception: null);
			//Debug.Log($"GetNetworkTime: {response.StatusCode}-{response.ReasonPhrase}");
			return Convert.ToBoolean(await response.Content.ReadAsStringAsync());
		}
		catch (HttpRequestException hre)
		{
			Advant.AdvAnalytics.LogFailureToDTD("get_tester_failure", hre);
			//Debug.Log($"GetTester: {hre.Message}");
			return false;
		}
		catch (Exception e)
		{
			//Debug.Log($"GetTester: {e.Message}");
			Advant.AdvAnalytics.LogFailureToDTD("get_tester_unexpected_failure", e);
			return false;
		}
	}

	public async UniTask<string> GetCountryAsync(int timeout)
	{
		try
		{
			Timeout = timeout == 0 ? 
				Timeout :
				TimeSpan.FromSeconds(timeout);
			
			var response = await GetAsync(_getCountryEndpoint, HttpCompletionOption.ResponseContentRead);
			response.EnsureSuccessStatusCode();
			Advant.AdvAnalytics.LogWebRequestToDTD("get_country",
													response.IsSuccessStatusCode,
													(int)response.StatusCode,
													response.ReasonPhrase,
													exception: null);
			//Debug.Log($"GetCountryAsync: {response.StatusCode}-{response.ReasonPhrase}");
			var jsonNode = JSONNode.Parse(await response.Content.ReadAsStringAsync());
			return jsonNode["country"];
		}
		catch (HttpRequestException hre)
		{
			//Debug.Log($"GetCountry: {hre.Message}");
			AdvAnalytics.LogFailureToDTD("get_country", hre);
		}
		catch (Exception e)
		{
			//Debug.Log($"GetCountry: {e.Message}");
			Advant.AdvAnalytics.LogFailureToDTD("get_country_unexpected_failure", e);
		}
		finally
		{
			Timeout = System.Threading.Timeout.InfiniteTimeSpan;
		}
		return null;
	}

	public async UniTask<UserIdResponse> GetOrCreateUserIdAsync(RegistrationToken dto)
	{
		var result = new UserIdResponse();
		try
		{
			var response = await PutAsync(_putUserIdEndpoint, new StringContent(dto.ToJson(), Encoding.UTF8, "application/json"));
			response.EnsureSuccessStatusCode();
			Advant.AdvAnalytics.LogWebRequestToDTD("get_user_id",
												  response.IsSuccessStatusCode,
											      (int) response.StatusCode,
												  response.ReasonPhrase,
												  exception: null);

			if (response.IsSuccessStatusCode)
			{
				var jsonNode = JSONNode.Parse(await response.Content.ReadAsStringAsync());
				result.UserId = jsonNode["userId"];
				result.SessionCount = jsonNode["sessionCount"];
			}
			else
			{
				result.UserId = -1;
			}
			//Debug.Log($"GetOrCreateUserId: {response.StatusCode}-{response.ReasonPhrase}");

			//Debug.LogWarning($"[ADVANAL] GetOrCreateUserIdAsync. UserId = {result.UserId}, SessionCount = {result.SessionCount}");
		}
		catch (Exception e)
		{
			AdvAnalytics.LogFailureToDTD("get_user_id", e);
			//Debug.Log($"GetOrCreateUserId: {e.Message}");
			result.UserId = -1;
		}
		return result;
	}

	public async UniTask<bool> PutSessionCount(long userId, long sessionCount)
    {
		var result = false;
		try
		{
			var response = await PutAsync(
				_putSessionCountEndpoint, 
				new StringContent($"{{\"UserId\":{userId},\"SessionCount\":{sessionCount}}}", Encoding.UTF8, "application/json"));
			response.EnsureSuccessStatusCode();
			AdvAnalytics.LogWebRequestToDTD("put_session_count",
												response.IsSuccessStatusCode,
												(int) response.StatusCode,
												response.ReasonPhrase,
												null);
			result = Convert.ToBoolean(await response.Content.ReadAsStringAsync());
		}
		catch (Exception e)
		{
			AdvAnalytics.LogFailureToDTD("put_session_count", e);
		}
		return result;
	}
}
}
