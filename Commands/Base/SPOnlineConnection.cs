﻿using Microsoft.Identity.Client;
using Microsoft.SharePoint.Client;
using Newtonsoft.Json;
using SharePointPnP.PowerShell.Commands.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Net.Http;
using System.Reflection;
using System.Web;

namespace SharePointPnP.PowerShell.Commands.Base
{
    public class SPOnlineConnection
    {
        internal static string DeviceLoginAppId = "31359c7f-bd7e-475c-86db-fdb8c937548e";

        private string _accessToken;
        private string _refreshToken;
        internal Assembly coreAssembly;
        internal string userAgent;
        internal string PnPVersionTag { get; set; }
        internal static List<ClientContext> ContextCache { get; set; }

        public static AuthenticationResult AuthenticationResult { get; set; }

        public static SPOnlineConnection CurrentConnection { get; internal set; }
        public ConnectionType ConnectionType { get; protected set; }
        public int MinimalHealthScore { get; protected set; }
        public int RetryCount { get; protected set; }
        public int RetryWait { get; protected set; }
        public PSCredential PSCredential { get; protected set; }

        public string Url { get; protected set; }

        public string TenantAdminUrl { get; protected set; }

        public ClientContext Context { get; set; }
        internal string AccessToken
        {
            get
            {
                if (!string.IsNullOrEmpty(_accessToken) && DateTime.Now > ExpiresOn && !string.IsNullOrEmpty(RefreshToken))
                {
                    // Expired token
                    var client = new HttpClient();
                    var uri = new Uri(Url);
                    var url = $"{uri.Scheme}://{uri.Host}";
                    var body = new StringContent($"resource={url}&client_id={DeviceLoginAppId}&grant_type=refresh_token&refresh_token={_refreshToken}");
                    body.Headers.ContentType.MediaType = "application/x-www-form-urlencoded";
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                    var result = client.PostAsync("https://login.microsoftonline.com/common/oauth2/token", body).GetAwaiter().GetResult();
                    var tokens = JsonConvert.DeserializeObject<Dictionary<string, string>>(result.Content.ReadAsStringAsync().GetAwaiter().GetResult());
                    _accessToken = tokens["access_token"];
                    _refreshToken = tokens["refresh_token"];
                    ExpiresOn = DateTime.Now.AddSeconds(int.Parse(tokens["expires_in"]));
                    //var credmgr = new CredentialManager(_moduleBase);
                    ///credmgr.Add(Url, _accessToken, _refreshToken, ExpiresOn);
                }
                return _accessToken;
            }
            set
            {
                _accessToken = value;
            }
        }

        internal string RefreshToken
        {
            get
            {
                return _refreshToken;
            }
            set
            {
                _refreshToken = value;
            }
        }

        internal DateTime ExpiresOn { get; set; }

        public SPOnlineConnection(ClientContext context, ConnectionType connectionType, int minimalHealthScore, int retryCount, int retryWait, PSCredential credential, string url, string tenantAdminUrl, string pnpVersionTag)
        {
            var coreAssembly = Assembly.GetExecutingAssembly();
            userAgent = $"NONISV|SharePointPnP|PnPPS/{((AssemblyFileVersionAttribute)coreAssembly.GetCustomAttribute(typeof(AssemblyFileVersionAttribute))).Version}";
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            Context = context;
            Context.ExecutingWebRequest += Context_ExecutingWebRequest;
            ConnectionType = connectionType;
            MinimalHealthScore = minimalHealthScore;
            RetryCount = retryCount;
            RetryWait = retryWait;
            PSCredential = credential;
            TenantAdminUrl = tenantAdminUrl;
            ContextCache = new List<ClientContext> { context };
            PnPVersionTag = pnpVersionTag;
            Url = (new Uri(url)).AbsoluteUri;
        }

        public SPOnlineConnection(ClientContext context, string accessToken, string refreshToken, DateTime expiresOn, ConnectionType connectionType, int minimalHealthScore, int retryCount, int retryWait, PSCredential credential, string url, string tenantAdminUrl, string pnpVersionTag)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            AccessToken = accessToken;
            RefreshToken = refreshToken;
            ExpiresOn = expiresOn;
            var coreAssembly = Assembly.GetExecutingAssembly();
            userAgent = $"NONISV|SharePointPnP|PnPPS/{((AssemblyFileVersionAttribute)coreAssembly.GetCustomAttribute(typeof(AssemblyFileVersionAttribute))).Version}";
            Context = context;
            Context.ExecutingWebRequest += Context_ExecutingWebRequest;
            ConnectionType = connectionType;
            MinimalHealthScore = minimalHealthScore;
            RetryCount = retryCount;
            RetryWait = retryWait;
            PSCredential = credential;
            TenantAdminUrl = tenantAdminUrl;
            ContextCache = new List<ClientContext> { context };
            PnPVersionTag = pnpVersionTag;
            Url = (new Uri(url)).AbsoluteUri;
            context.ExecutingWebRequest += (sender, args) =>
            {
                args.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + CurrentConnection.AccessToken;
            };
        }


        private void Context_ExecutingWebRequest(object sender, WebRequestEventArgs e)
        {
            e.WebRequestExecutor.WebRequest.UserAgent = userAgent;
        }

        public void RestoreCachedContext(string url)
        {
            Context = ContextCache.FirstOrDefault(c => HttpUtility.UrlEncode(c.Url) == HttpUtility.UrlEncode(url));
        }

        internal void CacheContext()
        {
            var c = ContextCache.FirstOrDefault(cc => HttpUtility.UrlEncode(cc.Url) == HttpUtility.UrlEncode(Context.Url));
            if (c == null)
            {
                ContextCache.Add(Context);
            }
        }

        public ClientContext CloneContext(string url)
        {
            var context = ContextCache.FirstOrDefault(c => HttpUtility.UrlEncode(c.Url) == HttpUtility.UrlEncode(url));
            if (context == null)
            {
                context = Context.Clone(url);
                ContextCache.Add(context);
            }
            Context = context;
            return context;
        }

        internal static ClientContext GetCachedContext(string url)
        {
            return ContextCache.FirstOrDefault(c => HttpUtility.UrlEncode(c.Url) == HttpUtility.UrlEncode(url));
        }

        internal static void ClearContextCache()
        {
            ContextCache.Clear();
        }

    }
}
