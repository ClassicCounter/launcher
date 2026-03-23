using System.Net.Http;

namespace Wauncher.Utils
{
    public static class HttpClientFactory
    {
        private static readonly Lazy<HttpClient> _sharedClient = new(() => new HttpClient());
        
        public static HttpClient Shared => _sharedClient.Value;
    }
}
