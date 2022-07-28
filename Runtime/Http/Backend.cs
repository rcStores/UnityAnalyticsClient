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

namespace Advant.Http
{
    internal sealed class Backend
    {
        private readonly Dictionary<Type, string> _gameDataEndpointsByType = new Dictionary<Type, string>();
        
        private string _getTesterEndpoint;
        private string _putUserIdEndpoint;

        public long UserId { get; private set; }

        public void SetPathBase(string pathBase)
        {
			_getTesterEndpoint = pathBase + "/AnalyticsData/GetTester";
			_putUserIdEndpoint = pathBase + "/UserIds/GetOrCreateUserId";
			_gameDataEndpointsByType[typeof(GameProperty)] = pathBase + "/AnalyticsData/SaveProperties";
			_gameDataEndpointsByType[typeof(GameEvent)] = pathBase + "/AnalyticsData/SaveEvents";
        }

        public async Task SendToServerAsync<T>(long userId, Cache<T> data) where T : IGameData // => v?
        {
            Log.Info("Task runs in thread #" + Thread.CurrentThread);
            if (data.IsEmpty())
                throw new ArgumentException("The cache is empty");

            await ExecuteWebRequestAsync(_gameDataEndpointsByType[typeof(T)], RequestType.POST, data.ToJson(userId));
        }

        public bool GetTester(long userId)
        {
            return Convert.ToBoolean(ExecuteWebRequestAsync(_getTesterEndpoint + $"/{userId}", RequestType.GET));
        }

        public async Task<UserIdResponse> GetOrCreateUserIdAsync(Identifier dto)
        {
			var result = new UserIdResponse();
            try
            {
                var jsonNode = JSONNode.Parse(await ExecuteWebRequestAsync(_putUserIdEndpoint, RequestType.PUT, dto.ToJson()));
                result.UserId = jsonNode["userId"];
                result.IsUserNew = jsonNode["isUserNew"];
            }
            catch (Exception e)
            {
                Log.Info(e.Message);
                result.UserId = -1;
            }
            return result;
            
        }

        private async Task<string> ExecuteWebRequestAsync(string path, RequestType type, string jsonData = null)
        {
            var request = CreateRequest(path, type, jsonData);
            var operation = request.SendWebRequest();

            while (!operation.isDone)
                await Task.Yield();
            if (request.responseCode != 201 && request.responseCode != 200)
            {
                throw new Exception(
					"Http request failure. Response code: " + request.responseCode + 
					"\nError: " + request.error +
					"\nDownload handler error (if any): " + request.downloadHandler.error);
            }
            return request.downloadHandler.text;
        }

        private UnityWebRequest CreateRequest(string path, RequestType type = RequestType.GET, string jsonData = null)
        {
            var request = new UnityWebRequest(path, type.ToString());

            if (jsonData != null)
            {
                var bodyRaw = Encoding.UTF8.GetBytes(jsonData);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            }

            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

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
