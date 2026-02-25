using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace helvety.screenshots.Views
{
    public sealed partial class SaveLocationPage : Page
    {
        private string _saveFolderPath = string.Empty;
        private bool _hasValidSaveFolder;

        public SaveLocationPage()
        {
            InitializeComponent();
            LoadAndValidateSaveFolder();
        }

        private void LoadAndValidateSaveFolder()
        {
            var settings = SettingsService.Load();
            _saveFolderPath = !string.IsNullOrWhiteSpace(settings.SaveFolderPath)
                ? settings.SaveFolderPath
                : SettingsService.GetDefaultDesktopFolderPath();

            UpdateFolderState();
        }

        private void UpdateFolderState()
        {
            if (SettingsService.TryValidateWritableFolder(_saveFolderPath, out var validationError))
            {
                _hasValidSaveFolder = true;
                SettingsService.SaveFolderPath(_saveFolderPath);
                SaveFolderText.Text = $"Save Folder: {_saveFolderPath}";
                SaveFolderStatusText.Text = string.Empty;
                return;
            }

            _hasValidSaveFolder = false;
            SaveFolderText.Text = $"Save Folder: {_saveFolderPath}";
            SaveFolderStatusText.Text = $"Choose a writable folder ({validationError}).";
        }

        private async void ChooseSaveFolderButton_Click(object sender, RoutedEventArgs e)
        {
            ChooseSaveFolderButton.IsEnabled = false;
            SaveFolderStatusText.Text = "Choosing folder...";

            try
            {
                var folderPicker = new FolderPicker
                {
                    SuggestedStartLocation = PickerLocationId.Desktop
                };
                folderPicker.FileTypeFilter.Add("*");

                if (App.MainAppWindow is null)
                {
                    SaveFolderStatusText.Text = "Unable to open folder picker.";
                    return;
                }

                var windowHandle = WindowNative.GetWindowHandle(App.MainAppWindow);
                InitializeWithWindow.Initialize(folderPicker, windowHandle);

                var selectedFolder = await folderPicker.PickSingleFolderAsync();
                if (selectedFolder is null)
                {
                    SaveFolderStatusText.Text = _hasValidSaveFolder
                        ? string.Empty
                        : "Choose a writable folder.";
                    return;
                }

                var candidatePath = selectedFolder.Path;
                if (!SettingsService.TryValidateWritableFolder(candidatePath, out var validationError))
                {
                    SaveFolderStatusText.Text = _hasValidSaveFolder
                        ? $"Folder not writable ({validationError})."
                        : $"Choose a writable folder ({validationError}).";
                    return;
                }

                _saveFolderPath = candidatePath;
                SettingsService.SaveFolderPath(_saveFolderPath);
                _hasValidSaveFolder = true;
                SaveFolderText.Text = $"Save Folder: {_saveFolderPath}";
                SaveFolderStatusText.Text = string.Empty;
            }
            catch (Exception ex)
            {
                SaveFolderStatusText.Text = $"Could not set folder ({ex.Message}).";
            }
            finally
            {
                ChooseSaveFolderButton.IsEnabled = true;
            }
        }
    }
}
