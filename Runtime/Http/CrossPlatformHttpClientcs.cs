using AndroidUtils;
using Advant;
using System.Threading;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading.Tasks;
using Advant.Http;
using System;
using SimpleJSON;
using Advant.Data.Models;
using Advant.Data;
using System.Collections;
using System.Globalization;
using System.Text;

using UnityEngine;

namespace Advant.Http
{
	public class HttpResponse
    {
        public string data;
        public int code;
        public string message;
        public string error;
    }
	
	internal class CrossPlatformHttpClient : IHttpClient
	{
		private readonly Dictionary<Type, string> _gameDataEndpointsByType = new Dictionary<Type, string>();
		
		private string _getTesterEndpoint;
		private string _getNetworkTimeEndpoint;
		private string _getCountryEndpoint;
		private string _putUserIdEndpoint;
		private string _putSessionCountEndpoint;
	
		public void SetPathBases(string analytics, string registration)
		{
			_getTesterEndpoint = registration + "/Registration/GetTester";
			_getNetworkTimeEndpoint = registration + "/Registration/GetNetworkTime";
			_getCountryEndpoint = "http://ip-api.com/json/"; //"https://ipapi.co/country/";
			_putUserIdEndpoint = registration + "/Registration/GetOrCreateUserId2";
			_putSessionCountEndpoint = registration + "/Sessions/PutSessionCount";
			_gameDataEndpointsByType[typeof(GameProperty)] = analytics + "/AnalyticsData/SendProperties";
			_gameDataEndpointsByType[typeof(GameEvent)] = analytics + "/AnalyticsData/SendEvents";
			_gameDataEndpointsByType[typeof(Session)] = analytics + "/Sessions/SaveSession";
		}
		
		public async UniTask<DataSendingResult> SendToServerAsync<TGameData>(string json)
		{
			DataSendingResult result = null;
			HttpResponse response = null;
			try
			{
#if UNITY_ANDROID
				await Task.Run(
					() => AndroidWebRequestWrapper.PostAsync(
					_gameDataEndpointsByType[typeof(TGameData)],
					json,
					(string data, int code, string message, string error) => {
						response = new HttpResponse
						{
							code = code,
							data = data,
							message = message,
							error = error
						};
					}))
					.AsUniTask();
				if (response == null) throw new Exception("Result of SendToServerAsync is null");
				
				result = new DataSendingResult
				{
					IsSuccess = response.code == 201 || response.code == 200,
					StatusCode = response.code,
					RequestError = response.error,
					ExceptionMessage = response.message
				};
#endif
			}
			catch (Exception e)
			{
				result.IsSuccess = false;
				result.ExceptionMessage = $"Message: {e.Message}\nInner exception message: {e.InnerException?.Message}";
			
				//Debug.LogWarning($"SendToServerAsync: {result.StatusCode}-{result.RequestError}\n{result.ExceptionMessage}");
				AdvAnalytics.LogFailureToDTD("send_to_server_failure", e, typeof(TGameData));
			}
			
			return result;	
		}
		
		public async UniTask<Tuple<bool, DateTime>> GetNetworkTime(CancellationToken token, int timeout = 0)
		{
			HttpResponse response = null;
			try
			{
#if UNITY_ANDROID
				var isCancelled = await Task.Run(
					() => AndroidWebRequestWrapper.GetAsync(
						_getNetworkTimeEndpoint,
						(string data, int code, string message, string error) => {
							response = new HttpResponse
							{
								code = code,
								data = data,
								message = message,
								error = error
							};
						}),
					token)
					.AsUniTask()
					.SuppressCancellationThrow();
				
				if (isCancelled)
					return Tuple.Create(true, DateTime.MinValue);
				
				if (response == null)  
					throw new Exception("Result of GetNetworkTime is null");
				else if (response.code != 201 && response.code != 200)
					throw new Exception($"GetNetworkTime returned bad status code {response.code}");
				
				DateTime.TryParseExact(response.data,
								   "yyyy-MM-ddTHH:mm:ss.fff",
								   CultureInfo.InvariantCulture,
								   DateTimeStyles.None,
								   out DateTime result);
								   
				Advant.AdvAnalytics.LogWebRequestToDTD("get_network_time",
													response.code == 201 || response.code == 200,
													response.code,
													response.message,
													exception: null);
													
				return Tuple.Create(false, result);
#endif
			}
			catch (Exception e)
			{
				Debug.LogWarning($"GetNetworkTime: {e.Message}");
				AdvAnalytics.LogFailureToDTD("get_time_failure", e);
			}
			
			return Tuple.Create(false, DateTime.MinValue);
		}
		
		public async UniTask<bool> GetTester(long userId)
		{
			DataSendingResult result = new DataSendingResult();
			HttpResponse response = null;
			
			try
			{
#if UNITY_ANDROID
				await Task.Run(
					() => AndroidWebRequestWrapper.GetAsync(
					_getNetworkTimeEndpoint,
					(string data, int code, string message, string error) => {
						response = new HttpResponse
						{
							code = code,
							data = data,
							message = message,
							error = error
						};
					}))
					.AsUniTask();
					
				if (response == null)  
					throw new Exception("Result of GetNetworkTime is null");
				else if (response.code != 201 && response.code != 200)
					throw new Exception($"GetNetworkTime returned bad status code {response.code}");
#endif
				
				Advant.AdvAnalytics.LogWebRequestToDTD("get_tester",
													response.code == 201 || response.code == 200,
													response.code,
													response.message,
													exception: null);
				Debug.LogWarning($"GetTester: {response.code}-{response.message}");
				return Convert.ToBoolean(response.data);
			}
			catch (Exception e)
			{
				Advant.AdvAnalytics.LogFailureToDTD("get_tester_failure", e);
				Debug.LogWarning($"GetTester: {e.Message}");
				return false;
			}
		}
		
		public async UniTask<string> GetCountryAsync(int timeout)
		{
			HttpResponse response = null;
			try
			{
#if UNITY_ANDROID
				await Task.Run(
					() => AndroidWebRequestWrapper.GetAsync(
					_getCountryEndpoint,
					(string data, int code, string message, string error) => {
						response = new HttpResponse
						{
							code = code,
							data = data,
							message = message,
							error = error
						};
					}))
					.AsUniTask();
					
				if (response == null)  
					throw new Exception("Result of GetCountryAsync is null");
				else if (response.code != 201 && response.code != 200)
					throw new Exception($"GetCountryAsync returned bad status code {response.code}");
#endif
				
				Advant.AdvAnalytics.LogWebRequestToDTD("get_country",
													response.code == 201 || response.code == 200,
													response.code,
													response.message,
													exception: null);
				Debug.LogWarning($"GetCountryAsync: {response.code}-{response.message}");
				return response.data;
			}
			catch (Exception e)
			{
				Advant.AdvAnalytics.LogFailureToDTD("get_country_failure", e);
				Debug.LogWarning($"GetTester: {e.Message}");
				return null;
			}
		}
		
		public async UniTask<UserIdResponse> GetOrCreateUserIdAsync(RegistrationToken dto)
		{
			var result = new UserIdResponse();
			HttpResponse response = null;
			try
			{
#if UNITY_ANDROID
				await Task.Run(
					() => AndroidWebRequestWrapper.PutAsync(
					_putUserIdEndpoint,
					dto.ToJson(),
					(string data, int code, string message, string error) => {
						response = new HttpResponse
						{
							code = code,
							data = data,
							message = message,
							error = error
						};
					}))
					.AsUniTask();
					
				if (response == null)  
					throw new Exception("Result of GetOrCreateUserIdAsync is null");
				else if (response.code != 201 && response.code != 200)
					throw new Exception($"GetOrCreateUserIdAsync returned bad status code {response.code}");
#endif

				Advant.AdvAnalytics.LogWebRequestToDTD("get_user_id",
													response.code == 201 || response.code == 200,
													response.code,
													response.message,
													exception: null);

				var jsonNode = JSONNode.Parse(response.data);
				result.UserId = jsonNode["userId"];
				result.SessionCount = jsonNode["sessionCount"];
				
				Debug.Log($"GetOrCreateUserId: {response.code}-{response.message}");
				Debug.LogWarning($"[ADVANAL] GetOrCreateUserIdAsync. UserId = {result.UserId}, SessionCount = {result.SessionCount}");
			}
			catch (Exception e)
			{
				AdvAnalytics.LogFailureToDTD("get_user_id", e);
				Debug.Log($"GetOrCreateUserId: {e.Message}");
				result.UserId = -1;
			}
			return result;
		}
		
		public async UniTask<bool> PutSessionCount(long userId, long sessionCount)
		{
			var result = false;
			HttpResponse response = null;
			try
			{
#if UNITY_ANDROID
				await Task.Run(
					() => AndroidWebRequestWrapper.PutAsync(
					_putSessionCountEndpoint,
					$"{{\"UserId\":{userId},\"SessionCount\":{sessionCount}}}",
					(string data, int code, string message, string error) => {
						response = new HttpResponse
						{
							code = code,
							data = data,
							message = message,
							error = error
						};
					}))
					.AsUniTask();
					
				if (response == null)  
					throw new Exception("Result of PutSessionCount is null");
				else if (response.code != 201 && response.code != 200)
					throw new Exception($"PutSessionCount returned bad status code: {response.code}");
#endif
					
				AdvAnalytics.LogWebRequestToDTD("put_session_count",
												response.code == 201 || response.code == 200,
												response.code,
												response.message,
												exception: null);
				result = Convert.ToBoolean(response.data);
			}
			catch (Exception e)
			{
				AdvAnalytics.LogFailureToDTD("put_session_count", e);
			}
			return result;
		}
	}
}