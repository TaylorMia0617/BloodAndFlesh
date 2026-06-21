using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Unity.UOS.Auth;
using Unity.UOS.Common;
using UnityEngine;

namespace Unity.UOS.Networking
{
    public static class PingServiceManager
    {
        private const string PingServicesPath = "/v1/game/ping-services";
        private const string SdkName = "Ping-Service-Manager";
        private const int TestCount = 5;

        private static HttpClient _httpClient;
        private static readonly Dictionary<string, PingServiceEndpoint> Endpoints = new();

        /// <summary>
        /// 初始化PingServiceManager, 该方法会尝试获取该UOS App对应的区域延迟Endpoint
        /// </summary>
        /// <param name="timeout">http client timeout</param>
        public static async Task Initialize(int timeout = HttpClient.DefaultTimeout)
        {
            await Initialize(Settings.AppID, Settings.AppSecret, timeout);
        }

        /// <summary>
        /// 初始化PingServiceManager, 该方法会尝试获取该UOS App对应的区域延迟Endpoint
        /// </summary>
        /// <param name="appID">UOS App ID</param>
        /// <param name="timeout">http client timeout</param>
        /// <param name="appSecret">UOS App Secret</param>
        public static async Task Initialize(string appID, string appSecret, int timeout = HttpClient.DefaultTimeout)
        {
            _httpClient ??= new HttpClient(new HttpClientOptions
            {
                SdkName = SdkName,
                SdkVersion = AuthTokenManager.SdkVersion,
                Authenticator = new UosNonceAuthenticator(appID, appSecret),
                BaseUrl = Networking.Endpoints.MultiverseEndpoint,
                Timeout = timeout
            });
            await RefreshPingEndpoints();
        }

        /// <summary>
        /// 刷新区域延迟Endpoints列表
        /// </summary>
        public static async Task RefreshPingEndpoints()
        {
            Endpoints.Clear();
            var resp = await _httpClient.Get<PingServiceResponse>(PingServicesPath);
            foreach (var endpoint in resp.pingServiceEndpoints)
            {
                Endpoints.Add(endpoint.regionName, endpoint);
            }
        }

        /// <summary>
        /// 获取区域延迟Endpoints列表
        /// </summary>
        /// <returns>区域延迟Endpoints列表, key为区域名称</returns>
        public static Dictionary<string, PingServiceEndpoint> GetPingEndpoints()
        {
            return Endpoints;
        }

        /// <summary>
        /// 获取客户端到区域的平均延迟, 延迟测试5次取平均数
        /// </summary>
        /// <returns>区域延迟Dictionary</returns>
        public static async Task<Dictionary<string, long>> GetPing()
        {
            var udpClient = new UdpClient();
            var latencies = new Dictionary<string, long>();
            try
            {
                foreach (var endpoint in Endpoints)
                {
                    var total = 0L;
                    for (var i = 0; i < TestCount; i++)
                    {
                        udpClient.Connect(endpoint.Value.udpEndpoint, endpoint.Value.udpPort);
                        var sendBytes = Encoding.ASCII.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());
                        await udpClient.SendAsync(sendBytes, sendBytes.Length);

                        var remoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
                        var receiveBytes = udpClient.Receive(ref remoteIpEndPoint);
                        var returnData = Encoding.ASCII.GetString(receiveBytes);

                        var timestamp = long.Parse(returnData);
                        var latency = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - timestamp;
                        total += latency;
                    }
                    latencies[endpoint.Value.regionName] = (long)Math.Round((float)total / TestCount);
                }
            }
            catch (Exception e ) {
                Debug.LogException(e);
            }

            return latencies;
        }
    }

    [Serializable]
    public class PingServiceResponse
    {
        public string gameId;
        public List<PingServiceEndpoint> pingServiceEndpoints;
    }

    [Serializable]
    public class PingServiceEndpoint
    {
        public string regionId;
        public string httpEndpoint;
        public int httpPort;
        public string udpEndpoint;
        public int udpPort;
        public string regionName;
    }
}
