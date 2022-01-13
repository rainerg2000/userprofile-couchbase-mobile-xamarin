using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UserProfileDemo.Core.Services;
using Xamarin.Android.Net;
using Xamarin.Forms;

[assembly: Dependency(typeof(UserProfileDemo.Droid.DependencyService.AuthAndroid))]
namespace UserProfileDemo.Droid.DependencyService
{
    public class AuthAndroid : IAuth
    {

        public async Task<string> GetSgSessionToken(Uri baseUri)
        {
            var accessToken = await GetAccessToken();
            var httpClientHandler = new AndroidClientHandler();
            httpClientHandler.CookieContainer = new CookieContainer();
            httpClientHandler.UseCookies = true;

            var httpClientDbServer = new HttpClient(httpClientHandler);
            httpClientDbServer.MaxResponseContentBufferSize = 100000;
            httpClientDbServer.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            var uri = new Uri($"{baseUri}/_session");
            var wsPostReq = await httpClientDbServer.PostAsync(uri, null);

            if (wsPostReq.StatusCode != HttpStatusCode.OK)
            {
                return null;
            }

            var cookies = httpClientHandler.CookieContainer.GetCookies(uri);
            if (cookies["SyncGatewaySession"] == null)
            {
                return null;
            }

            var sessionToken = cookies["SyncGatewaySession"].Value;

            if (string.IsNullOrEmpty(sessionToken))
            {
                return null;
            }

            return sessionToken;
        }

        private HttpClient CreateHttpClientWithBasicAuthHeader(TimeSpan timeout, string clientId, string clientSecret)
        {
            var base64String = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            var authHeader = new AuthenticationHeaderValue("Basic", base64String);

            var httpClientHandler = new AndroidClientHandler();
            httpClientHandler.CookieContainer = new CookieContainer();
            httpClientHandler.UseCookies = true;

            var client = new HttpClient(httpClientHandler);
            client.DefaultRequestHeaders.Authorization = authHeader;
            client.MaxResponseContentBufferSize = 20000;
            client.Timeout = timeout;
            return client;
        }

        private async Task<string> GetAccessToken()
        {
            try
            {
                var content = new StringContent(
                  $"grant_type=password&username=couchbase-support-43188&password=nvkdhngorndkjglclpslsj",
                  Encoding.UTF8,
                  "application/x-www-form-urlencoded");


                var httpClientWithBasicAuthHeader = CreateHttpClientWithBasicAuthHeader(TimeSpan.FromSeconds(30), "nro-dev", "1fde0529-2aea-45b2-8dd8-bd4848a65cbb");
                
                //var wsPostReq = await httpClientWithBasicAuthHeader.PostAsync("https://keycloak.intg-3.msei.nro.biotronik.dev/auth/realms/nro/protocol/openid-connect/token", content);
                var wsPostReq = await httpClientWithBasicAuthHeader.PostAsync("https://sso.nsc-msei.kubernetes-1.perftest-homemonitoring.com/auth/realms/nro/protocol/openid-connect/token", content);
                // ReSharper disable once StyleCop.SA1305 (Hungarian)
                var wsPostRespBody = await wsPostReq.Content.ReadAsStringAsync();
                if (!wsPostReq.IsSuccessStatusCode)
                {
                    return null;
                }

                var responseProps = JObject.Parse(wsPostRespBody);
                var receivedRefreshToken = responseProps.Value<string>("refresh_token");
                var receivedAccessToken = responseProps.Value<string>("access_token");
                if (string.IsNullOrEmpty(receivedRefreshToken) || string.IsNullOrEmpty(receivedAccessToken))
                {
                    return null;
                }

                return receivedAccessToken;
            }
            catch (Exception e)
            {
                return null;
            }
        }
    }
}