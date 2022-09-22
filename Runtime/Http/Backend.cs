using Advant.Data.Models;
using Advant.Data;
using Advant.Logging;

using System;
using System.Collections.Generic;
using UnityEngine.Networking;
using System.Text;
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

        public long UserId { get; private set; }

        public void SetPathBases(string analytics, string registration)
        {
			_getTesterEndpoint 								= registration + "/Registration/GetTester";
			_getNetworkTimeEndpoint							= registration + "Registration/GetNetworkTime";
			_getCountryEndpoint 							= "https://ipapi.co/json";
			_putUserIdEndpoint 								= registration + "/Registration/GetOrCreateUserId";
			_putSessionCountEndpoint 						= registration + "/Sessions/PutSessionCount";
			_gameDataEndpointsByType[typeof(GameProperty)]	= analytics + "/AnalyticsData/SendProperties";
			_gameDataEndpointsByType[typeof(GameEvent)] 	= analytics + "/AnalyticsData/SendEvents";
			_gameDataEndpointsByType[typeof(Session)] 		= registration + "/Sessions/SaveSession";
        }

        public async UniTask<bool> SendToServerAsync<T>(string data)
        {
            Log.Info("Task runs in thread #" + Thread.CurrentThread);
            if (String.IsNullOrEmpty(data))
				return false;
                //throw new ArgumentException("The cache is empty");
			try
			{
				await ExecuteWebRequestAsync(_gameDataEndpointsByType[typeof(T)], RequestType.POST, data);
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
		
		public async UniTask<DateTime> GetNetworkTime()
		{
			string response;
			try
			{
				response = await ExecuteWebRequestAsync(_getNetworkTimeEndpoint, RequestType.GET);
			}
			catch (Exception e)
			{
				Debug.Log("Error while getting tester info: " + e.Message);
				response = default(DateTime).ToString();;
			}
				
			Debug.LogWarning("GetNetworkTime response: " + response);
            return DateTime.Parse(response);
		}

        public async UniTask<bool> GetTester(long userId)
        {
			string response = null;
			try
			{
				response = await ExecuteWebRequestAsync(_getTesterEndpoint + $"/{userId}", RequestType.GET);
			}
			catch (Exception e)
			{
				Debug.Log("Error while getting tester info: " + e.Message);
				response = "false";
			}
				
			Debug.LogWarning("GetTester response: " + response);
            return Convert.ToBoolean(response);
        }
		
		public async UniTask<string> GetCountryAsync(int timeout)
		{
			string country = null;	
			try
			{
				var jsonNode = JSONNode.Parse(await ExecuteWebRequestAsync(_getCountryEndpoint, RequestType.GET, null, timeout));
				country = jsonNode["country"];
			}
			catch (Exception e)
			{
				Debug.Log("Error while getting country info: " + e.Message);
			}
            return country;		
		}

        public async UniTask<UserIdResponse> GetOrCreateUserIdAsync(Identifier dto)
        {
			var result = new UserIdResponse();
            try
            {
                var jsonNode = JSONNode.Parse(await ExecuteWebRequestAsync(_putUserIdEndpoint, RequestType.PUT, dto.ToJson()));
                result.UserId = jsonNode["userId"];
                result.SessionCount = jsonNode["sessionCount"];
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
					await ExecuteWebRequestAsync(_putSessionCountEndpoint, RequestType.PUT, $"{{\"UserId\":{userId},\"SessionCount\":{sessionCount}}}"));
            }
            catch (Exception e)
            {
                Log.Info(e.Message);
            }
            return result;         
        }

        private async UniTask<string> ExecuteWebRequestAsync(string path, RequestType type, string jsonData = null, int timeout = 0)
        {
			using var request = CreateRequest(path, type, jsonData, timeout);
			UnityWebRequest operation = null;
			try
			{
				operation = await request.SendWebRequest();
			}
			catch (Exception e)
			{
				File.WriteAllText(
					Path.Combine(Application.persistentDataPath, "UploadHandlerData"), 
					Encoding.UTF8.GetString(request.uploadHandler.data));
				throw e;
			}
				
            // while (!operation.isDone)
				// await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);
                //await Task.Yield();
            // if (operation.responseCode != 201 && operation.responseCode != 200)
            // {
				// File.WriteAllText(
					// Path.Combine(Application.persistentDataPath, "UploadHandlerData"), 
					// Encoding.UTF8.GetString(request.uploadHandler.data));

                // throw new Exception(
					// "Http request failure. Response code: " + operation.responseCode + 
					// "\nError: " + operation.error +
					// "\nUpdload handler data: " + Encoding.UTF8.GetString(operation.uploadHandler.data));
            // }
            return operation.downloadHandler.text;
        }

        private UnityWebRequest CreateRequest(string path, RequestType type = RequestType.GET, string jsonData = null, int timeout = 0)
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

            return request;
        }

        private void AttachHeader(UnityWebRequest request, string key, string value)
        {
            request.SetRequestHeader(key, value);
        }
    }

    internal enum RequestType
    {
        GET = 0,
        POST = 1,
        PUT = 2
    }
}

