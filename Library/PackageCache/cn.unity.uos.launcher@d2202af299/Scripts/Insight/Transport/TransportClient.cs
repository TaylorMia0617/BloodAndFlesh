using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.UOS.Common;
using Unity.UOS.Networking;
using HttpClient = Unity.UOS.Networking.HttpClient;

namespace Unity.UOS.Insight.Transport
{
    public static class TransportClient
    {
        public static void Initialize(int timeout = 10)
        {
            _httpClient ??= new HttpClient(new HttpClientOptions
            {
                SdkName = InsightSDK.LIB_NAME,
                SdkVersion = InsightSDK.LIB_VERSION,
                Authenticator = new UosNonceAuthenticator(Settings.AppID, Settings.AppSecret),
                BaseUrl = Endpoints.InsightEndpoint,
                Timeout = timeout
            });
        }

        private static HttpClient _httpClient;

        public static async Task<TrackResponse> Track(string jsonRequest)
        {
            return await _httpClient.Post<TrackResponse>("track", jsonRequest);
        }

        public static async Task<TrackResponse> Track(TrackRequest trackRequest)
        {
            var req = JsonConvert.SerializeObject(trackRequest);
            return await Track(req);
        }

        public static async Task<Project> GetProject()
        {
            return await _httpClient.Get<Project>("project");
        }
    }
}