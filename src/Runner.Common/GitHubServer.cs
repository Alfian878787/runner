using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GitHub.Services.Common;
using GitHub.Services.WebApi;

namespace GitHub.Runner.Common
{

    public class GitHubResult
    {
        public HttpStatusCode StatusCode { get; set; }
        public String Message { get; set; }
        public HttpResponseHeaders Headers { get; set; }
    }


    [ServiceLocator(Default = typeof(GitHubServer))]
    public interface IGitHubServer : IRunnerService
    {
        Task ConnectAsync(Uri GithubUrl, string AccessToken);
        Task<GitHubResult> RevokeInstallationToken();
    }

    public class GitHubServer : RunnerService, IGitHubServer
    {
        private Uri githubUrl;
        private string accessToken;
        public async Task ConnectAsync(Uri GithubUrl, string AccessToken)
        {
            githubUrl = GithubUrl;
            accessToken = AccessToken;

            var requestUrl = new UriBuilder(githubUrl);
            requestUrl.Path = requestUrl.Path.TrimEnd('/') + "/meta";

            using (var httpClientHandler = HostContext.CreateHttpClientHandler())
            using (var httpClient = HttpClientFactory.Create(httpClientHandler, new VssHttpRetryMessageHandler(3)))
            using (HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl.Uri))
            {
                requestMessage.Headers.Add("Accept", "application/vnd.github.v3+json");
                httpClient.DefaultRequestHeaders.UserAgent.Add(HostContext.UserAgent);

                var base64EncodingToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"x-access-token:{accessToken}"));
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64EncodingToken);

                var response = await httpClient.SendAsync(requestMessage, CancellationToken.None);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
                else
                {
                    var content = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Fail to authenticate with GitHub: {response.StatusCode} ({response.ReasonPhrase})");
                }
            }
        }

        public async Task<GitHubResult> RevokeInstallationToken()
        {
            var result = new GitHubResult();
            var requestUrl = new UriBuilder(githubUrl);
            requestUrl.Path = requestUrl.Path.TrimEnd('/') + "/installation/token";

            using (var httpClientHandler = HostContext.CreateHttpClientHandler())
            using (var httpClient = HttpClientFactory.Create(httpClientHandler, new VssHttpRetryMessageHandler(3)))
            using (HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Delete, requestUrl.Uri))
            {
                requestMessage.Headers.Add("Accept", "application/vnd.github.gambit-preview+json");
                httpClient.DefaultRequestHeaders.UserAgent.Add(HostContext.UserAgent);

                var base64EncodingToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"x-access-token:{accessToken}"));
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64EncodingToken);

                var response = await httpClient.SendAsync(requestMessage, CancellationToken.None);

                result.StatusCode = response.StatusCode;
                result.Headers = response.Headers;
                result.Message = await response.Content.ReadAsStringAsync();
            }

            return result;
        }
    }
}
