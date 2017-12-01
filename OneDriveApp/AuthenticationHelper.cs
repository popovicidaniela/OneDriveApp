using Microsoft.Graph;
using System;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Identity.Client;

namespace OneDriveApp
{
    public class AuthenticationHelper
    {
        static string clientId = "?";//yse your own clientId
        public static string[] Scopes = { "Files.ReadWrite.All" };
        public static PublicClientApplication IdentityClientApp = new PublicClientApplication(clientId);
        public static string TokenForUser = null;
        public static IUser AuthedIUser = null;
        public static DateTimeOffset Expiration;
        private static GraphServiceClient graphClient = null;

        public static GraphServiceClient GetAuthenticatedClient()
        {
            if (graphClient == null)
            {

                try
                {
                    graphClient = new GraphServiceClient(
                        "https://graph.microsoft.com/v1.0",
                        new DelegateAuthenticationProvider(
                            async (requestMessage) =>
                            {
                                var token = await GetTokenForUserAsync();
                                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("bearer", token);
                            }));
                    return graphClient;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Could not create a graph client: " + ex.Message);
                }
            }
            return graphClient;
        }

        /// <summary>
        /// Get Token for User.
        /// </summary>
        /// <returns>Token for user.</returns>
        public static async Task<string> GetTokenForUserAsync()
        {
            AuthenticationResult authResult;
            try
            {
                authResult = await IdentityClientApp.AcquireTokenSilentAsync(Scopes, AuthedIUser);
                TokenForUser = authResult.AccessToken;
            }
            catch (Exception)
            {
                if (TokenForUser == null || Expiration <= DateTimeOffset.UtcNow.AddMinutes(5))
                {
                    authResult = await IdentityClientApp.AcquireTokenAsync(Scopes);
                    TokenForUser = authResult.AccessToken;
                    AuthedIUser = authResult.User;
                    Expiration = authResult.ExpiresOn;
                }
            }
            return TokenForUser;
        }

        /// <summary>
        /// Signs the user out of the service.
        /// </summary>
        public static void SignOut()
        {
            graphClient = null;
            TokenForUser = null;
        }
    }
}
