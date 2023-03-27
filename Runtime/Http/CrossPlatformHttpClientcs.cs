using Advant.AndroidUtils;
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
		
		public UniTask<DataSendingResult> SendToServerAsync<TGameData>(string json)
		{
			HttpResponse response = null;
			try
			{
#if UNITY_ANDROID
				AndroidWebRequestWrapper.PostAsync(
					_gameDataEndpointsByType[typeof(TGameData)],
					json,
					(HttpResponse r) => {
						response = r;
					});
				if (response == null) throw new Exception("Result of SendToServerAsync is null");
				
				var result = new DataSendingResult
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
			
			return Task.FromResult(result).AsUniTask();	
		}
		
		public async UniTask<(bool, DateTime)> GetNetworkTime(CancellationToken token, int timeout = 0)
		{
			HttpResponse response = null;
			try
			{
#if UNITY_ANDROID
				var isCancelled = await Task.Run(
					() => AndroidWebRequestWrapper.GetAsync(
						_getNetworkTimeEndpoint,
						(HttpResponse r) => {
							response = r;
						}),
					token)
					.AsUniTask()
					.SuppressCancellationThrow();
				
				if (isCancelled)
					return Task.FromResult(Tuple.Create(true, DateTime.MinValue)).AsUniTask();
				
				if (response == null)  
					throw new Exception("Result of GetNetworkTime is null");
				else if (response.code != 201 && response.code != 200)
					throw new Exception("GetNetworkTime returned bad status code");
				
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
													
				return Task.FromResult(Tuple.Create(false, result)).AsUniTask();
#endif
			}
			catch (Exception e)
			{
				result.IsSuccess = false;
				result.ExceptionMessage = $"Message: {e.Message}\nInner exception message: {e.InnerException?.Message}";
			
				Debug.LogWarning($"SendToServerAsync: {e.Message}");
				AdvAnalytics.LogFailureToDTD("get_time_failure", e);
			}
			
			return Tuple.Create(false, DateTime.MinValue);
		}
		
		public UniTask<bool> GetTester(long userId)
		{
			DataSendingResult result = new DataSendingResult();
			HttpResponse response = null;
			
			try
			{
#if UNITY_ANDROID
				AndroidWebRequestWrapper.GetAsync(
					_getNetworkTimeEndpoint,
					(HttpResponse r) => {
						response = r;
					});
					
				if (response == null)  
					throw new Exception("Result of GetNetworkTime is null");
				else if (response.code != 201 && response.code != 200)
					throw new Exception("GetNetworkTime returned bad status code");
#endif
				
				Advant.AdvAnalytics.LogWebRequestToDTD("get_tester",
													response.code == 201 || response.code == 200,
													response.code,
													response.message,
													exception: null);
				Debug.LogWarning($"GetTester: {response.code}-{response.message}");
				return Task.FromResult(Convert.ToBoolean(response.data)).AsUniTask();
			}
			catch (Exception e)
			{
				Advant.AdvAnalytics.LogFailureToDTD("get_tester_failure", e);
				Debug.LogWarning($"GetTester: {e.Message}");
				return Task.FromResult(false).AsUniTask();
			}
		}
		
		public UniTask<string> GetCountryAsync(int timeout)
		{
			HttpResponse response = null;
			try
			{
#if UNITY_ANDROID
				AndroidWebRequestWrapper.GetAsync(
					_getCountryEndpoint,
					(HttpResponse r) => {
						response = r;
					});
					
				if (response == null)  
					throw new Exception("Result of GetCountryAsync is null");
				else if (response.code != 201 && response.code != 200)
					throw new Exception("GetCountryAsync returned bad status code");
#endif
				
				Advant.AdvAnalytics.LogWebRequestToDTD("get_country",
													response.code == 201 || response.code == 200,
													response.code,
													response.message,
													exception: null);
				Debug.LogWarning($"GetCountryAsync: {response.code}-{response.message}");
				return Task.FromResult(Convert.ToBoolean(response.data)).AsUniTask();
			}
			catch (Exception e)
			{
				Advant.AdvAnalytics.LogFailureToDTD("get_country_failure", e);
				Debug.LogWarning($"GetTester: {e.Message}");
				return Task.FromResult(null).AsUniTask();HttpResponse response = null;
			}
		}
		
		public UniTask<UserIdResponse> GetOrCreateUserIdAsync(RegistrationToken dto)
		{
			var result = new UserIdResponse();
			HttpResponse response = null;
			try
			{
#if UNITY_ANDROID
				AndroidWebRequestWrapper.PutAsync(
					_putUserIdEndpoint,
					dto.ToJson(),
					(HttpResponse r) => {
						response = r;
					});
					
				if (response == null)  
					throw new Exception("Result of GetOrCreateUserIdAsync is null");
				else if (response.code != 201 && response.code != 200)
					throw new Exception("GetOrCreateUserIdAsync returned bad status code");
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
			return Task.FromResult(result).AsUniTask();
		}
		
		public UniTask<bool> PutSessionCount(long userId, long sessionCount)
		{
			var result = false;
			HttpResponse response = null;
			try
			{
#if UNITY_ANDROID
				AndroidWebRequestWrapper.PutAsync(
					_putSessionCountEndpoint,
					$"{{\"UserId\":{userId},\"SessionCount\":{sessionCount}}}",
					(HttpResponse r) => {
						response = r;
					});
					
				if (response == null)  
					throw new Exception("Result of PutSessionCount is null");
				else if (response.code != 201 && response.code != 200)
					throw new Exception("PutSessionCount returned bad status code");
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
			return Task.FromResult(result).AsUniTask();
		}
	}
}