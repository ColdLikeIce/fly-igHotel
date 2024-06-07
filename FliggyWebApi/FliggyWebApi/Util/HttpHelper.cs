using Microsoft.AspNetCore.Mvc;
using Serilog;
using System.Net;
using System;
using System.Text;
using System.Net.Http;
using System.Threading;

namespace FliggyWebApi.Util
{
    public class HttpHelper
    {
        private static readonly HttpClient HttpClient;

        static HttpHelper()
        {
            HttpClient = new HttpClient();
            HttpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/102.0.0.0 Safari/537.36");
        }

        public static string HttpGet(string url)
        {
            // 创建HttpWebRequest对象
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET"; // 设置请求方法为GET

            // 发送请求并获取响应
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                // 获取响应流
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    // 读取响应内容
                    string responseText = reader.ReadToEnd();
                    return responseText;
                }
            }
        }

        /// <summary>
        /// 发起GET异步请求
        /// </summary>
        /// <param name="url"></param>
        /// <param name="headers"></param>
        /// <param name="contentType"></param>
        /// <returns></returns>
        public static async Task<string> HttpGetAsync(string url, Dictionary<string, string>? headers = null, int timeOut = 10)
        {
            using (HttpClient client = new HttpClient())
            {
                //client.Timeout = TimeSpan.FromSeconds(timeOut);
                if (headers != null)
                {
                    foreach (var header in headers)
                        client.DefaultRequestHeaders.Add(header.Key, header.Value);
                }
                HttpResponseMessage response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
        }

        /// <summary>
        /// 发起POST同步请求
        ///
        /// </summary>
        /// <param name="url"></param>
        /// <param name="postData"></param>
        /// <param name="contentType">application/xml、application/json、application/text、application/x-www-form-urlencoded</param>
        /// <param name="headers">填充消息头</param>
        /// <returns></returns>
        public static string HttpPost(string url, string postData, string contentType, int timeOut = 30, Dictionary<string, string>? headers = null, string cookie = "")
        {
            postData = postData ?? "";
            using (HttpContent httpContent = new StringContent(postData, Encoding.Default))
            {
                return HttpPost(url, httpContent, contentType, timeOut, headers, cookie);
            }
        }

        public static string HttpPost(string url, HttpContent postData, string contentType, int timeOut = 30, Dictionary<string, string>? headers = null, string cookie = "")
        {
            using (HttpClient httpClient = new HttpClient(new HttpClientHandler() { UseCookies = false }))
            {
                if (headers != null)
                {
                    httpClient.DefaultRequestHeaders.Clear();
                    foreach (var header in headers)
                    {
                        if (header.Key.Contains("content-type"))
                        {
                            continue;
                        }
                        httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
                    }
                }
                else
                {
                    httpClient.DefaultRequestHeaders.Clear();
                }
                if (!string.IsNullOrEmpty(contentType))
                    postData.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

                if (!string.IsNullOrWhiteSpace(cookie))
                {
                    httpClient.DefaultRequestHeaders.Add("Cookie", cookie);
                }

                httpClient.Timeout = new TimeSpan(0, 0, timeOut);
                HttpResponseMessage response = httpClient.PostAsync(url, postData).Result;
                foreach (var item in response.Headers)
                {
                    if (item.Key == "Set-Cookie")
                    {
                        var coo = item.Value;
                        foreach (var key in coo)
                        {
                            Log.Information($"{key}");
                        }
                    }
                }
                return response.Content.ReadAsStringAsync().Result;
            }
        }
    }
}