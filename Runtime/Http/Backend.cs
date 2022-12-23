using Advant.Data.Models;
using Advant.Data;
using Advant.Logging;

using System;
using System.Collections.Generic;
using UnityEngine.Networking;
using System.Text;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine;
using System.Threading;
using SimpleJSON;
using System.IO;
using Cysharp.Threading.Tasks;

namespace Advant.Http
{
    internal sealed class Backend
    {
        private readonly Dictionary<Type, string> _gameDataEndpointsByType = new Dictionary<Type, string>();
        
        private string _getTesterEndpoint;
		private string _getNetworkTimeEndpoint;
		private string _getCountryEndpoint;
        private string _putUserIdEndpoint;
		private string _putSessionCountEndpoint;
		
		private const string CANCELLED_REQUEST = "CANCELLED";
		
#region Helper data types definition

		public enum RequestType
		{
			GET = 0,
			POST = 1,
			PUT = 2
		}
	
		public class CertificateWhore : CertificateHandler
		{
			protected override bool ValidateCertificate(byte[] certificateData)
			{
				return true;
			}
		}
		
#endregion

#region Init

        public void SetPathBases(string analytics, string registration)
        {
			_getTesterEndpoint 								= registration + "/Registration/GetTester";
			_getNetworkTimeEndpoint							= registration + "/Registration/GetNetworkTime";
			_getCountryEndpoint 							= "http://ip-api.com/json/"; //"https://ipapi.co/country/";
			_putUserIdEndpoint 								= registration + "/Registration/GetOrCreateUserId2";
			_putSessionCountEndpoint 						= registration + "/Sessions/PutSessionCount";
			_gameDataEndpointsByType[typeof(GameProperty)]	= analytics + "/AnalyticsData/SendProperties";
			_gameDataEndpointsByType[typeof(GameEvent)] 	= analytics + "/AnalyticsData/SendEvents";
			_gameDataEndpointsByType[typeof(Session)] 		= registration + "/Sessions/SaveSession";
        }
		
#endregion

#region Public request executors

        public async UniTask<DataSendingResult> SendToServerAsync<T>(string data)
        {
            Log.Info("Task runs in thread #" + Thread.CurrentThread);
            if (String.IsNullOrEmpty(data))
				return new DataSendingResult() { IsSuccess = false, ExceptionMessage = "Serialized game data string is null or empty" };
                //throw new ArgumentException("The cache is empty");
			try
			{
				return await ExecuteWebRequestAsync(_gameDataEndpointsByType[typeof(T)], 
											 RequestType.POST, 
											 CancellationToken.None, 
											 jsonData: data, 
											 timeout: 0, 
											 certificateHandler: new CertificateWhore());
			}
			catch (Exception e)
			{
				Debug.LogWarning("[ADVANAL] Error while sending data: " + e.Message);
				Debug.LogWarning("Stack trace: " + e.StackTrace);
				Debug.LogWarning("Source: " + e.Source);
				AdvAnalytics.LogFailureToDTD("send_to_server_failure", e, typeof(T));
				return new DataSendingResult() { ExceptionMessage = e.Message };
			}
			//return true;
        }
		
		public async UniTask<(bool, DateTime)> GetNetworkTime(CancellationToken token, int timeout = 0)
		{
			string response = null;
			DataSendingResult requestResult = null;
			try
			{
				requestResult = await ExecuteWebRequestAsync(_getNetworkTimeEndpoint, 
																RequestType.GET,
																token,
																jsonData: null, 
																timeout: 0, 
																certificateHandler: new CertificateWhore());
				AdvAnalytics.LogWebRequestToDTD("get_network_time", 
													requestResult.IsSuccess, 
													requestResult.StatusCode, 
													requestResult.RequestError, 
													requestResult.ExceptionMessage);
				if (requestResult != null && requestResult.IsSuccess)
					response = requestResult.DownloadHandler;
			}
			catch (Exception e)
			{
				AdvAnalytics.LogFailureToDTD("get_time_failure", e);
				Debug.Log("Error while getting network time: " + e.Message);
				return (false, default);
			}
			
			if (requestResult.DownloadHandler == CANCELLED_REQUEST)
				return (true, default);
				
			//Debug.LogWarning("GetNetworkTime response: " + response);

			DateTime result;
			DateTime.TryParseExact(response, 
								   "yyyy-MM-ddTHH:mm:ss.fff", 
								   CultureInfo.InvariantCulture, 
								   DateTimeStyles.None, 
								   out result);
			return (false, result);
		}

        public async UniTask<bool> GetTester(long userId)
        {
			string response = null;
			DataSendingResult requestResult = null;
			try
			{
				requestResult = await ExecuteWebRequestAsync(_getTesterEndpoint + $"/{userId}", 
														RequestType.GET, 
														CancellationToken.None, 
														jsonData: null, 
														timeout: 0, 
														certificateHandler: new CertificateWhore());
				if (requestResult.IsSuccess)
					response = requestResult.DownloadHandler;
				else
					response = "false";
			}
			catch (Exception e)
			{
				AdvAnalytics.LogFailureToDTD("get_tester", e);
				Debug.Log("Error while getting tester info: " + e.Message);
				response = "false";
			}
				
			//Debug.LogWarning("GetTester response: " + response);
            return Convert.ToBoolean(response);
        }
		
		public async UniTask<string> GetCountryAsync(int timeout)
		{
			string country = null;	
			try
			{
				var requestResult = await ExecuteWebRequestAsync(_getCountryEndpoint, RequestType.GET, CancellationToken.None, jsonData: null, timeout);
				AdvAnalytics.LogWebRequestToDTD("get_country", 
													requestResult.IsSuccess, 
													requestResult.StatusCode, 
													requestResult.RequestError, 
													requestResult.ExceptionMessage);
				
				if (requestResult.IsSuccess)
				{
					var jsonNode = JSONNode.Parse(requestResult.DownloadHandler);
					country = jsonNode["country"];	
				}				
				// country = await ExecuteWebRequestAsync(_getCountryEndpoint, 
													   // RequestType.GET, 
													   // CancellationToken.None, 
													   // jsonData: null, 
													   // timeout);
			}
			catch (Exception e)
			{
				AdvAnalytics.LogFailureToDTD("get_country", e);
				Debug.Log("Error while getting country info: " + e.Message);
			}
            return country;		
		}
		
		// public async UniTask<long> GetCurrentSessionCount(long userId)
		// {
			// string count;	
			// try
			// {
				// count = Convert.ToInt64(await ExecuteWebRequestAsync(_getSessionCountEndpoint + $"/{userId}", RequestType.GET));
			// }
			// catch (Exception e)
			// {
				// Debug.Log("Error while getting current session count: " + e.Message);
			// }
            // return count;	
		// }
			

        public async UniTask<UserIdResponse> GetOrCreateUserIdAsync(RegistrationToken dto)
        {
			var result = new UserIdResponse();
            try
            {
				var requestResult = await ExecuteWebRequestAsync(_putUserIdEndpoint, 
																RequestType.PUT, 
																CancellationToken.None, 
																dto.ToJson());
				AdvAnalytics.LogWebRequestToDTD("get_user_id", 
													requestResult.IsSuccess, 
													requestResult.StatusCode, 
													requestResult.RequestError, 
													requestResult.ExceptionMessage);
				
				if (requestResult.IsSuccess)
				{
					var jsonNode = JSONNode.Parse(requestResult.DownloadHandler);
					result.UserId = jsonNode["userId"];
					result.SessionCount = jsonNode["sessionCount"];
				}
				else 
				{
					result.UserId = -1;
				}
                
				//Debug.LogWarning($"[ADVANAL] GetOrCreateUserIdAsync. UserId = {result.UserId}, SessionCount = {result.SessionCount}");
            }
            catch (Exception e)
            {
				AdvAnalytics.LogFailureToDTD("get_user_id", e);
				Debug.LogWarning("Error while sending registration request: " + e.Message);
                Log.Info(e.Message);
                result.UserId = -1;
            }
            return result;         
        }
		
		public async UniTask<bool> PutSessionCount(long userId, long sessionCount)
        {
			var result = false;
            try
            {
				var requestResult = await ExecuteWebRequestAsync(_putSessionCountEndpoint, 
																RequestType.PUT, 
																CancellationToken.None,
																$"{{\"UserId\":{userId},\"SessionCount\":{sessionCount}}}", 
																timeout: 0, 
																certificateHandler: new CertificateWhore());
				AdvAnalytics.LogWebRequestToDTD("put_session_count", 
													requestResult.IsSuccess, 
													requestResult.StatusCode, 
													requestResult.RequestError, 
													requestResult.ExceptionMessage);
                result = Convert.ToBoolean(requestResult.DownloadHandler);
            }
            catch (Exception e)
            {
				AdvAnalytics.LogFailureToDTD("put_session_count", e);
                Log.Info(e.Message);
            }
            return result;         
        }

#endregion

#region Implementation
        
		private async UniTask<DataSendingResult> ExecuteWebRequestAsync(string path, 
															 RequestType type,
															 CancellationToken token,
															 string jsonData = null, 
															 int timeout = 0, 
															 CertificateHandler certificateHandler = null)
        {
			using var request = CreateRequest(path, type, jsonData, timeout, certificateHandler);
			var result = new DataSendingResult();
			UnityWebRequest operation = null;
			try
			{
				var (isCancelled, resultReq) = await request.SendWebRequest()
					.WithCancellation(token)
					.SuppressCancellationThrow();
					
				operation = resultReq;
				
				if (isCancelled) 
					result.DownloadHandler = CANCELLED_REQUEST;
				else if (operation.downloadHandler == null)
					AdvAnalytics.LogMessageToDTD($"empty_download_handler: {path}");
				else
					result.DownloadHandler = operation.downloadHandler.text;
				
				result.IsSuccess = operation.responseCode == 200 || operation.responseCode == 201;
				result.StatusCode = operation.responseCode;
				result.RequestError = operation.error;
			}
			catch (Exception e)
			{
				result.IsSuccess = false;
				result.StatusCode = request.responseCode;
				result.RequestError = request.error;
				result.ExceptionMessage = e.Message;
				//Debug.LogError($"Executing web-request {path} {result.StatusCode}: {result.ExceptionMessage},\n{result.RequestError}");
				AdvAnalytics.LogFailureToDTD($"web_request: {path}", e);
				// if (path == _getCountryEndpoint)
				// {
					// Debug.Log($"GetCountry response:\nCode = {request.responseCode}, result = {request.result}, error = {request.error}");
					// if (request.responseCode == 429)
						// Debug.Log($"DownloadHandler: {operation.downloadHandler.text}");
				// }
				
				//File.WriteAllText(
					//Path.Combine(Application.persistentDataPath, "UploadHandlerData"), 
					//Encoding.UTF8.GetString(request.uploadHandler.data));
					
				//throw e;
			}
			// if (path == _getCountryEndpoint)
				// Debug.Log($"GetCountry response:\nCode = {operation.responseCode}, result = {operation.result}");
            return result;
        }

        private UnityWebRequest CreateRequest(string path, RequestType type, string jsonData, int timeout, CertificateHandler certificateHandler)
        {
            var request = new UnityWebRequest(path, type.ToString());

            if (jsonData != null)
            {
                var bodyRaw = Encoding.UTF8.GetBytes(jsonData);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            }

            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
			request.timeout = timeout;
			request.certificateHandler = certificateHandler;

            return request;
        }

        private void AttachHeader(UnityWebRequest request, string key, string value)
        {
            request.SetRequestHeader(key, value);
        }
		
	}
	
#endregion
}

