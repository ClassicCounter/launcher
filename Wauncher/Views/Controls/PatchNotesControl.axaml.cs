using Avalonia.Controls;
using Avalonia.Threading;
using Newtonsoft.Json;
using System.IO;
using System.Net.NetworkInformation;
using System.Numerics;
using System.Text.RegularExpressions;
using Wauncher.Utils;
using Wauncher.ViewModels;

namespace Wauncher.Views.Controls
{
    public partial class PatchNotesControl : UserControl
    {
        public PatchNotesControl()
        {
            InitializeComponent();
        }

        private static string PatchNotesCachePath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ClassicCounter",
                "Wauncher",
                "cache",
                "patchnotes.md");

        public async Task LoadPatchNotesAsync()
        {
            try
            {
                if (DataContext is MainWindowViewModel vm && vm.IsOfflineMode)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        PatchNotesVersion.IsVisible = false;
                        PatchNotesList.ItemsSource = LoadCachedPatchNotes();
                        PatchNotesScroll.Offset = new Avalonia.Vector(0, 0);
                    });
                    return;
                }

                var markdown = await Api.GitHub.GetPatchNotesWauncher();
                var items = ParsePatchNotes(markdown);
                SavePatchNotesCache(markdown);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    PatchNotesVersion.IsVisible = false;
                    PatchNotesList.ItemsSource = items;
                    PatchNotesScroll.Offset = new Avalonia.Vector(0, 0);
                });
            }
            catch
            {
                var items = LoadCachedPatchNotes();
                if (items.Count == 0)
                    items = BuildFallbackPatchNotes();

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    PatchNotesVersion.IsVisible = false;
                    PatchNotesList.ItemsSource = items;
                    PatchNotesScroll.Offset = new Avalonia.Vector(0, 0);
                });
            }
        }

        private static void SavePatchNotesCache(string markdown)
        {
            try
            {
                var directory = Path.GetDirectoryName(PatchNotesCachePath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(PatchNotesCachePath, markdown);
            }
            catch
            {
            }
        }

        private static List<PatchNoteItem> LoadCachedPatchNotes()
        {
            try
            {
                if (!File.Exists(PatchNotesCachePath))
                    return new List<PatchNoteItem>();

                return ParsePatchNotes(File.ReadAllText(PatchNotesCachePath));
            }
            catch
            {
                return new List<PatchNoteItem>();
            }
        }

        private static List<PatchNoteItem> BuildFallbackPatchNotes()
        {
            return new List<PatchNoteItem>
            {
                new() { Text = "Wauncher Update", IsMajorHeader = true },
                new() { Text = "Patch notes are temporarily unavailable.", IsBullet = true },
            };
        }

        private static List<PatchNoteItem> ParsePatchNotes(string markdown)
        {
            var items = new List<PatchNoteItem>();
            var lastWasMajorHeader = false;

            foreach (var rawLine in markdown.Split('\n'))
            {
                var line = rawLine.TrimEnd();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                line = line.Trim();
                line = line.Replace("**", "").Replace("__", "");
                line = Regex.Replace(line, @"\[(.*?)\]\((.*?)\)", "$1");
                line = Regex.Replace(line, @"`([^`]*)`", "$1");

                if (line.StartsWith("# "))
                {
                    var headerText = line.TrimStart('#', ' ');
                    var (title, dateText) = SplitPatchTitleAndDate(headerText);

                    items.Add(new PatchNoteItem
                    {
                        Text = title,
                        IsMajorHeader = true
                    });

                    if (!string.IsNullOrWhiteSpace(dateText))
                    {
                        items.Add(new PatchNoteItem
                        {
                            Text = dateText,
                            IsDateHeader = true
                        });
                    }

                    lastWasMajorHeader = true;
                    continue;
                }

                if (line.StartsWith("## "))
                {
                    items.Add(new PatchNoteItem
                    {
                        Text = line.TrimStart('#', ' '),
                        IsHeader = true
                    });
                    lastWasMajorHeader = false;
                    continue;
                }

                if (lastWasMajorHeader && LooksLikeStandaloneDate(line))
                {
                    items.Add(new PatchNoteItem
                    {
                        Text = line,
                        IsDateHeader = true
                    });
                    lastWasMajorHeader = false;
                    continue;
                }

                if (line.StartsWith("- ") || line.StartsWith("* "))
                {
                    items.Add(new PatchNoteItem
                    {
                        Text = line[2..].Trim(),
                        IsBullet = true
                    });
                    lastWasMajorHeader = false;
                    continue;
                }

                items.Add(new PatchNoteItem
                {
                    Text = line,
                    IsBullet = true
                });
                lastWasMajorHeader = false;
            }

            return items;
        }

        private static (string title, string date) SplitPatchTitleAndDate(string headerText)
        {
            var match = Regex.Match(
                headerText,
                @"^(?<title>.*?)(?:\s*[-|]\s*|\s{2,})(?<date>(?:jan|feb|mar|apr|may|jun|jul|aug|sep|sept|oct|nov|dec)[a-z]*\.?\s+\d{1,2},?\s+\d{4})$",
                RegexOptions.IgnoreCase);

            if (!match.Success)
                return (headerText, string.Empty);

            return (
                match.Groups["title"].Value.Trim(),
                match.Groups["date"].Value.Trim());
        }

        private static bool LooksLikeStandaloneDate(string line)
        {
            return Regex.IsMatch(
                line,
                @"^(?:jan|feb|mar|apr|may|jun|jul|aug|sep|sept|oct|nov|dec)[a-z]*\.?\s+\d{1,2},?\s+\d{4}$",
                RegexOptions.IgnoreCase);
        }
    }
}
