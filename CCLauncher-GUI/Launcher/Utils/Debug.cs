/*
 *  Utils/Debug.cs
 */

namespace Launcher.Utils
{
    public static class Debug
    {
        public static bool Enabled() => Argument.Exists("--debug-mode");
    }
}