using Microsoft.Win32;

namespace Wauncher.Utils
{
    public class ProtocolManager
    {
        public static void RegisterURIHandler()
        {   
            var appCurrentLocation = Path.Combine(new FileInfo(System.Environment.ProcessPath).Directory.FullName, "wauncher.exe");
            EnsureKeyExists(Registry.CurrentUser, "Software/Classes/cc", "ClassicCounter");
            SetValue(Registry.CurrentUser, "Software/Classes/cc", "URL Protocol", string.Empty);
            EnsureKeyExists(Registry.CurrentUser, "Software/Classes/cc/DefaultIcon", $"{appCurrentLocation},1");
            EnsureKeyExists(Registry.CurrentUser, "Software/Classes/cc/shell/open/command", $"\"{appCurrentLocation}\" --protocol-command \"%1\"");
        }

        private static void SetValue(RegistryKey rootKey, string keys, string valueName, string value)
        {
            var key = EnsureKeyExists(rootKey, keys);
            key.SetValue(valueName, value);
        }

        private static RegistryKey EnsureKeyExists(RegistryKey rootKey, string keys, string defaultValue = null)
        {
            if (rootKey == null)
            {
                throw new Exception("Root key is (null)");
            }

            var currentKey = rootKey;
            foreach (var key in keys.Split('/'))
            {
                currentKey = currentKey.OpenSubKey(key, RegistryKeyPermissionCheck.ReadWriteSubTree)
                             ?? currentKey.CreateSubKey(key, RegistryKeyPermissionCheck.ReadWriteSubTree);

                if (currentKey == null)
                {
                    throw new Exception("Could not get or create key");
                }
            }

            if (defaultValue != null)
            {
                currentKey.SetValue(string.Empty, defaultValue);
            }

            return currentKey;
        }
    }
}
