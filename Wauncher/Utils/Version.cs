using Newtonsoft.Json.Linq;
using System.Reflection;

namespace Wauncher.Utils
{
    public static class Version
    {
        public static string Current =>
            Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

        public async static Task<string> GetLatestVersion()
        {
            if (Debug.Enabled())
                Terminal.Debug("Getting latest version.");

            try
            {
                string responseString = await Api.GitHub.GetLatestRelease();
                JObject responseJson = JObject.Parse(responseString);

                if (responseJson["tag_name"] == null)
                    throw new Exception("\"tag_name\" doesn't exist in response.");

                var tag = ((string?)responseJson["tag_name"] ?? Current).Trim();
                if (tag.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                    tag = tag[1..];
                return string.IsNullOrWhiteSpace(tag) ? Current : tag;
            }
            catch
            {
                if (Debug.Enabled())
                    Terminal.Debug("Couldn't get latest version.");
            }

            return Current;
        }
    }
}
