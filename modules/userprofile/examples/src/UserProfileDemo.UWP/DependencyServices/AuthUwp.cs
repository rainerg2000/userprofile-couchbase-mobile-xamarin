using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Net.Http.Headers;
using Windows.Security.Authentication.Web;
using Windows.Security.Authentication.Web.Core;
using Windows.Security.Credentials;


using UserProfileDemo.Core.Services;
using Xamarin.Forms;
using Xamarin.Essentials;

[assembly: Dependency(typeof(UserProfileDemo.UWP.DependencyServices.AuthUwp))]
namespace UserProfileDemo.UWP.DependencyServices
{
    public class AuthUwp : IAuth
    {
        private string WebAccountProviderId = "https://login.microsoft.com";
        private string AzureAdTenantUri = "https://login.microsoftonline.com/mdpsemd.onmicrosoft.com";
        private string AzureAdClientId = "68fa2f6e-3099-4bcc-890e-6aa8fa6fcdb2";
        private string AzureAppIdForSyncGatewaySso = "https://mdp-azure-sso-2";


        public async Task<string> GetSgSessionToken(Uri baseUri)
        {
            var accessToken = await GetAccessToken();
            var httpClientHandler = new HttpClientHandler();
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

        private async Task<string> GetAccessToken()
        {
            try
            {
                WebAccount userAccount;
                WebAccountProvider wap;
                string azureAdAccessToken;

                wap = await WebAuthenticationCoreManager.FindAccountProviderAsync(WebAccountProviderId, AzureAdTenantUri);
                WebTokenRequest wtr = new WebTokenRequest(wap, "openid email", AzureAdClientId);
                wtr.Properties.Add("resource", AzureAppIdForSyncGatewaySso);

                // need this URI for setting up the App registration on Azure AD.
                // Go to https://portal.azure.com 'mdp by semd', Azure Active Directory, App Registrations, client resource, Settings, Redirect URIs
                // and enter the URI there.
                var applicationCallbackUri = WebAuthenticationBroker.GetCurrentApplicationCallbackUri();
                var currentAppCallbackUri = applicationCallbackUri.Host.ToUpper();
                var uri = string.Format("ms-appx-web://Microsoft.AAD.BrokerPlugIn/{0}", currentAppCallbackUri);

                // start by getting a token without user interaction. This should work unless it's the first time this app asks for these cloud permissions.
                var wtrr = await WebAuthenticationCoreManager.GetTokenSilentlyAsync(wtr);
                if (wtrr.ResponseStatus == WebTokenRequestStatus.Success)
                {
                    var responseData0 = wtrr.ResponseData[0];
                    userAccount = responseData0.WebAccount;
                    azureAdAccessToken = responseData0.Token;
                    var jwt = new JwtSecurityToken(azureAdAccessToken);
                    return azureAdAccessToken;
                }

                if (wtrr.ResponseStatus == WebTokenRequestStatus.UserInteractionRequired)
                {
                    var wtrr2 = await MainThread.InvokeOnMainThreadAsync(async () => await WebAuthenticationCoreManager.RequestTokenAsync(wtr));
                    if (wtrr2.ResponseStatus == WebTokenRequestStatus.Success)
                    {
                        var responseData0 = wtrr2.ResponseData[0];
                        userAccount = responseData0.WebAccount;
                        azureAdAccessToken = responseData0.Token;
                        var jwt = new JwtSecurityToken(azureAdAccessToken);
                        return azureAdAccessToken;
                    }

                    return null;
                }

                // In case of Azure auth issues we can't communicate with the backend,
                // let's use Analytics to report the issue, so that we can troubleshoot
                var analyticsAddlData = new Dictionary<string, object>()
                {
                    { "ResponseStatus", wtrr.ResponseStatus.ToString() },
                    { "ErrorCode", wtrr.ResponseError?.ErrorCode.ToString("X8") },
                    { "ErrorMessage", wtrr.ResponseError?.ErrorMessage },
                };
                if (wtrr.ResponseError.ErrorMessage.Contains("AADSTS50011"))
                {
                    analyticsAddlData.Add("RedirectUri", currentAppCallbackUri);
                }

                return null;
            }
            catch (Exception e)
            {
                return null;
            }
        }
    }
}