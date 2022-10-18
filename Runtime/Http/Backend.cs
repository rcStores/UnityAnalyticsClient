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
			_putUserIdEndpoint 								= registration + "/Registration/GetOrCreateUserId";
			_putSessionCountEndpoint 						= registration + "/Sessions/PutSessionCount";
			_gameDataEndpointsByType[typeof(GameProperty)]	= analytics + "/AnalyticsData/SendProperties";
			_gameDataEndpointsByType[typeof(GameEvent)] 	= analytics + "/AnalyticsData/SendEvents";
			_gameDataEndpointsByType[typeof(Session)] 		= registration + "/Sessions/SaveSession";
        }
		
#endregion

#region Public request executors

        public async UniTask<bool> SendToServerAsync<T>(string data)
        {
            Log.Info("Task runs in thread #" + Thread.CurrentThread);
            if (String.IsNullOrEmpty(data))
				return false;
                //throw new ArgumentException("The cache is empty");
			try
			{
				await ExecuteWebRequestAsync(_gameDataEndpointsByType[typeof(T)], 
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
				return false;
			}
			return true;
        }
		
		public async UniTask<(bool, DateTime)> GetNetworkTime(CancellationToken token, int timeout = 0)
		{
			string response = null;
			try
			{
				response = await ExecuteWebRequestAsync(_getNetworkTimeEndpoint, 
														RequestType.GET,
														token,
														jsonData: null, 
														timeout: 0, 
														certificateHandler: new CertificateWhore());
			}
			catch (Exception e)
			{
				Debug.Log("Error while getting network time: " + e.Message);
			}
			
			if (response == CANCELLED_REQUEST)
				return (true, default);
				
			//Debug.LogWarning("GetNetworkTime response: " + response);
            return response is null ? (false, default) : (false, DateTime.ParseExact(response, "yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture));
		}

        public async UniTask<bool> GetTester(long userId)
        {
			string response = null;
			try
			{
				response = await ExecuteWebRequestAsync(_getTesterEndpoint + $"/{userId}", 
														RequestType.GET, 
														CancellationToken.None, 
														jsonData: null, 
														timeout: 0, 
														certificateHandler: new CertificateWhore());
			}
			catch (Exception e)
			{
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
				var jsonNode = JSONNode.Parse(await ExecuteWebRequestAsync(_getCountryEndpoint, RequestType.GET, CancellationToken.None, jsonData: null, timeout));
				country = jsonNode["country"];
				
				// country = await ExecuteWebRequestAsync(_getCountryEndpoint, 
													   // RequestType.GET, 
													   // CancellationToken.None, 
													   // jsonData: null, 
													   // timeout);
			}
			catch (Exception e)
			{
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
			

        public async UniTask<UserIdResponse> GetOrCreateUserIdAsync(Identifier dto)
        {
			var result = new UserIdResponse();
            try
            {
                var jsonNode = JSONNode.Parse(await ExecuteWebRequestAsync(_putUserIdEndpoint, 
																		   RequestType.PUT, 
																		   CancellationToken.None, 
																		   dto.ToJson()));
                result.UserId = jsonNode["userId"];
                result.SessionCount = jsonNode["sessionCount"];
				//Debug.LogWarning($"[ADVANAL] GetOrCreateUserIdAsync. UserId = {result.UserId}, SessionCount = {result.SessionCount}");
            }
            catch (Exception e)
            {
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
                result = Convert.ToBoolean(
					await ExecuteWebRequestAsync(_putSessionCountEndpoint, 
												 RequestType.PUT, 
												 CancellationToken.None,
												 $"{{\"UserId\":{userId},\"SessionCount\":{sessionCount}}}", 
												 timeout: 0, 
												 certificateHandler: new CertificateWhore()));
            }
            catch (Exception e)
            {
                Log.Info(e.Message);
            }
            return result;         
        }

#endregion

#region Implementation
        
		private async UniTask<string> ExecuteWebRequestAsync(string path, 
															 RequestType type,
															 CancellationToken token,
															 string jsonData = null, 
															 int timeout = 0, 
															 CertificateHandler certificateHandler = null)
        {
			using var request = CreateRequest(path, type, jsonData, timeout, certificateHandler);
			string result = null;
			UnityWebRequest operation = null;
			try
			{
				var (isCancelled, resultReq) = await request.SendWebRequest()
					.WithCancellation(token)
					.SuppressCancellationThrow();
					
				operation = resultReq;
				
				if (isCancelled) 
					result = CANCELLED_REQUEST;
				else 
					result = operation.downloadHandler.text;
			}
			catch (Exception e)
			{
				// if (path == _getCountryEndpoint)
				// {
					// Debug.Log($"GetCountry response:\nCode = {request.responseCode}, result = {request.result}, error = {request.error}");
					// if (request.responseCode == 429)
						// Debug.Log($"DownloadHandler: {operation.downloadHandler.text}");
				// }
				
				// File.WriteAllText(
					// Path.Combine(Application.persistentDataPath, "UploadHandlerData"), 
					// Encoding.UTF8.GetString(request.uploadHandler.data));
				throw e;
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

