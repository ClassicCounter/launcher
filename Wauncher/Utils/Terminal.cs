using Spectre.Console;
using System.Reflection;

namespace Wauncher.Utils
{
    public static class Terminal
    {
        private static string _prefix = "[orange1]Classic[/][blue]Counter[/]";
        private static string _grey = "grey82";
        private static string _seperator = "[grey50]|[/]";
        private static readonly string _steamHappy = LoadSteamHappy();

        private static string LoadSteamHappy()
        {
            try
            {
                using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Wauncher.Assets.steamhappy.txt");
                if (stream == null)
                    return string.Empty;

                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            catch
            {
                return string.Empty;
            }
        }

        public static void Init()
        {
            AnsiConsole.MarkupLine($"{_prefix} {_seperator} [{_grey}]Wauncher maintained by [/][purple4_1]koolych[/][{_grey}][/]");
            AnsiConsole.MarkupLine($"{_prefix} {_seperator} [{_grey}]Coded by [/][lightcoral]heapy[/][{_grey}][/]");
            AnsiConsole.MarkupLine($"{_prefix} {_seperator} [{_grey}]https://github.com/ClassicCounter [/]");
            AnsiConsole.MarkupLine($"{_prefix} {_seperator} [{_grey}]Version: {Version.Current}[/]");
        }

        public static void Print(object? message)
            => AnsiConsole.MarkupLine($"{_prefix} {_seperator} [{_grey}]{Markup.Escape(message?.ToString() ?? string.Empty)}[/]");

        public static void Success(object? message)
            => AnsiConsole.MarkupLine($"{_prefix} {_seperator} [green1]{Markup.Escape(message?.ToString() ?? string.Empty)}[/]");

        public static void Warning(object? message)
            => AnsiConsole.MarkupLine($"{_prefix} {_seperator} [yellow]{Markup.Escape(message?.ToString() ?? string.Empty)}[/]");

        public static void Error(object? message)
            => AnsiConsole.MarkupLine($"{_prefix} {_seperator} [red]{Markup.Escape(message?.ToString() ?? string.Empty)}[/]");

        public static void Debug(object? message)
            => AnsiConsole.MarkupLine($"[purple]{Markup.Escape(message?.ToString() ?? string.Empty)}[/]");
        
        public static void SteamHappy() =>
            AnsiConsole.Write(_steamHappy);

        private static string Date()
            => $"[{_grey}]{DateTime.Now.ToString("HH:mm:ss")}[/]";
    }
}

