using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using System;

namespace Launcher.Utils
{
    public static class LauncherVersion
    {
        public static string Current = "2.2.3";

        public async static Task<string> GetLatestVersion()
        {
            /*if (Debug.Enabled())
                Terminal.Debug("Getting latest version.");*/

            try
            {
                string responseString = await Api.GitHub.GetLatestRelease();
                JObject responseJson = JObject.Parse(responseString);

                if (responseJson["tag_name"] == null)
                    throw new Exception("\"tag_name\" doesn't exist in response.");

                return (string?)responseJson["tag_name"] ?? Current;
            }
            catch
            {
                /*if (Debug.Enabled())
                    Terminal.Debug("Couldn't get latest version.");*/
            }

            return Current;
        }
    }
}
