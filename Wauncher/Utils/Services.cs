using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Wauncher.Utils
{
    public class Services
    {
        public static string GetMd5(string str)
        {
            if (String.IsNullOrEmpty(str))
            {
                return string.Empty;
            }
            try
            {
                var byteOld = Encoding.UTF8.GetBytes(str);
                var byteNew = MD5.HashData(byteOld);
                StringBuilder sb = new(32);
                foreach (var b in byteNew)
                {
                    sb.Append(b.ToString("x2"));
                }

                return sb.ToString();
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
        public static string GetExePath()
        {
            return Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
        }

        public static bool IsWindows() => OperatingSystem.IsWindows();
    }
}
