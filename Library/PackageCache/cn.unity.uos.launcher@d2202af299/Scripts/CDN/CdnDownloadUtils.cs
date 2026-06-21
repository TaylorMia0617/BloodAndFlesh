using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.UOS.Networking;
using HttpClient = Unity.UOS.Networking.HttpClient;

namespace Unity.UOS.Common
{
    /// <summary>
    /// UOS CDN下载链接获取工具
    /// </summary>
    public class CdnDownloadUtils
    {
        private static string _bucketId;
        
        private static string _badgeName;

        private static string _releaseId;

        private static bool _initialized = false;

        private static Dictionary<string, string> _fileMapping;
        
        private static string _cdnEndpoint;

        private static HttpClient _httpClient;

        /// <summary>
        /// 初始化并获取文件映射
        /// </summary>
        /// <param name="bucketId">资源桶id</param>
        /// <param name="badgeName">badge名，如果和release id同时为空，默认使用latest</param>
        /// <param name="releaseId">release id，有传值的话会优先使用</param>
        /// <returns>初始化是否成功</returns>
        public static async Task<bool> InitializeAsync(string bucketId, string badgeName, string releaseId)
        {
            if (_initialized)
            {
                return true;
            }
            
            if (string.IsNullOrEmpty(Settings.AppID) || string.IsNullOrEmpty(bucketId))
            {
                Debug.LogWarning("请先设置 uos app id 和 bucket id");
                return false;
            }

            _bucketId = bucketId;
            _releaseId = releaseId;
            _badgeName = string.IsNullOrEmpty(badgeName) ? "latest" : badgeName;
            
            _httpClient = new HttpClient(new HttpClientOptions
            {
                SdkName = "CDN Download",
                SdkVersion = "1.0.0",
                BaseUrl = Endpoints.CdnEndpoint
            });

            if (string.IsNullOrEmpty(_releaseId))
            {
                _releaseId = await GetReleaseIdAsync();
                if (string.IsNullOrEmpty(_releaseId))
                {
                    return false;
                }
            }
            
            await LoadFileMappingAsync();
            if (_fileMapping.TryGetValue("_uos_cdn_endpoint_", out _cdnEndpoint))
            {
                if (!string.IsNullOrEmpty(_cdnEndpoint))
                {
                    _initialized = true;
                    Debug.Log("cdn download utils initialized");
                    return true;
                }
            }

            return false;
        }
        
        /// <summary>
        /// 获取资源映关系
        /// </summary>
        /// <returns>资源映射表</returns>
        public static Dictionary<string, string> GetDownloadMapping()
        {
            if (!_initialized || _fileMapping == null || string.IsNullOrEmpty(_cdnEndpoint))
            {
                Debug.LogWarning("请先调用InitializeAsync()");
                return null;
            }
            
            return _fileMapping;
        }

        /// <summary>
        /// 获取文件下载链接
        /// </summary>
        /// <param name="fileName">文件名</param>
        /// <returns>下载链接</returns>
        public static string GetDownloadUrl(string fileName)
        {
            if (!_initialized || _fileMapping == null || string.IsNullOrEmpty(_cdnEndpoint))
            {
                Debug.LogWarning("请先调用InitializeAsync()");
                return "";
            }

            if (!_fileMapping.TryGetValue(fileName, out var filePath))
            {
                Debug.LogWarning($"未找到文件: {fileName}");
                return "";
            }

            return _cdnEndpoint + filePath;
        }

        /// <summary>
        /// 尝试获取文件下载链接
        /// </summary>
        /// <param name="fileName">文件名</param>
        /// <param name="downloadUrl">下载链接</param>
        /// <returns>是否成功获取</returns>
        public static bool TryGetDownloadUrl(string fileName, out string downloadUrl)
        {           
            downloadUrl = "";
            if (!_initialized || _fileMapping == null || string.IsNullOrEmpty(_cdnEndpoint))
            {
                Debug.LogWarning("请先调用InitializeAsync()");
                return false;
            }
            
            if (!_fileMapping.TryGetValue(fileName, out var filePath))
            {
                return false;
            }

            downloadUrl = _cdnEndpoint + filePath;
            return true;
        }
        
        /// <summary>
        /// 清除缓存
        /// </summary>
        public static void ClearCache()
        {
            _initialized = false;
            _fileMapping = new Dictionary<string, string>();
            _cdnEndpoint = "";
            _releaseId = "";
            _badgeName = "";
        }

        /// <summary>
        /// 通过 Badge 获取 Release ID
        /// </summary>
        private static async Task<string> GetReleaseIdAsync()
        {
            try
            {
                string url = $"/client_api/v1/buckets/{_bucketId}/release_by_badge/{_badgeName}/";
                var resp = await _httpClient.Get<ReleaseInfo>(url);
                if (!string.IsNullOrEmpty(resp.releaseid))
                {
                    return resp.releaseid;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"get release id by badge fail : {e.Message}");
            }

            return "";
        }

        /// <summary>
        /// 加载文件映射
        /// </summary>
        private static async Task LoadFileMappingAsync()
        {
            string url = $"/{Settings.AppID}/{_releaseId}.json";

            var tmpHttpClient = new HttpClient(new HttpClientOptions() { BaseUrl = Endpoints.WcsCdnEndpoint });
            try { 
                _fileMapping = await tmpHttpClient.Get<Dictionary<string, string>>(url);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"load mapping file fail : {e.Message}");
            }
        }
        
        
        [Serializable]
        internal struct ReleaseInfo
        {
            public string releaseid;
            public string releasenum;
        }
        
    }
}