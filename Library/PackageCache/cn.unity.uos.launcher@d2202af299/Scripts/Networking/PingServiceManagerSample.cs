using System;
using UnityEngine;

namespace Unity.UOS.Networking
{
    public class PingServiceManagerSample : MonoBehaviour
    {
        private async void Start()
        {
            try
            {
                await PingServiceManager.Initialize();
                Debug.Log("[PingServiceManagerSample] Initialized, fetching ping services...");

                var endpoints = PingServiceManager.GetPingEndpoints();
            
                foreach (var endpoint in endpoints)
                {
                    Debug.Log($"[PingServiceManagerSample] Region: {endpoint.Key} ({endpoint.Value.regionId}), " +
                              $"HTTP: {endpoint.Value.httpEndpoint}:{endpoint.Value.httpPort}, " +
                              $"UDP: {endpoint.Value.udpEndpoint}:{endpoint.Value.udpPort}");
                }
                var latencies = await PingServiceManager.GetPing();
                foreach (var latency in latencies)
                {
                    Debug.Log($"latency to region {latency.Key}: {latency.Value}ms");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[PingServiceManagerSample] Failed: {e.Message}");
            }
        }
    }
}
