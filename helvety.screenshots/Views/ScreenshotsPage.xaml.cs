using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System.IO;
using System.Linq;
using Microsoft.UI.Xaml;

namespace helvety.screenshots.Views
{
    public sealed partial class ScreenshotsPage : Page
    {
        private const string DefaultHotkeyDisplay = "Shift+S";

        public ScreenshotsPage()
        {
            InitializeComponent();
            SettingsService.SaveFolderPathChanged += SettingsService_SaveFolderPathChanged;
            Unloaded += ScreenshotsPage_Unloaded;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            RefreshEmptyStateMessage();
        }

        private void SettingsService_SaveFolderPathChanged()
        {
            DispatcherQueue.TryEnqueue(RefreshEmptyStateMessage);
        }

        private void ScreenshotsPage_Unloaded(object sender, RoutedEventArgs e)
        {
            SettingsService.SaveFolderPathChanged -= SettingsService_SaveFolderPathChanged;
            Unloaded -= ScreenshotsPage_Unloaded;
        }

        private void RefreshEmptyStateMessage()
        {
            var settings = SettingsService.Load();
            var folderPath = !string.IsNullOrWhiteSpace(settings.SaveFolderPath)
                ? settings.SaveFolderPath
                : SettingsService.GetDefaultDesktopFolderPath();
            var hotkeyDisplay = settings.Hotkey?.Display ?? DefaultHotkeyDisplay;

            EmptyStateMessageText.Text = $"Press {hotkeyDisplay} to create your first screenshot.";

            var isEmptyFolder = Directory.Exists(folderPath) &&
                                !Directory.EnumerateFileSystemEntries(folderPath).Any();
            EmptyFolderCallout.Visibility = isEmptyFolder
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }
}
