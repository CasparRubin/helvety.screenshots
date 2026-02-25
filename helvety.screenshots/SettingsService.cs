using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation.Collections;
using Windows.Storage;

namespace helvety.screenshots
{
    internal static class SettingsService
    {
        private const uint DefaultHotkeyModifiers = 0x0004; // Shift
        private const uint DefaultHotkeyVirtualKey = 0x53; // S
        private const string DefaultHotkeyDisplay = "Shift+S";

        internal static event Action? SaveFolderPathChanged;
        internal static event Action? SettingsChanged;

        private const int CurrentSettingsVersion = 1;
        private const string DefaultScreenshotsFolderName = "Screenshots (Helvety)";
        private const string SettingsVersionKey = "SettingsVersion";
        private const string SaveFolderPathKey = "SaveFolderPath";
        private const string HotkeyModifiersKey = "HotkeyModifiers";
        private const string HotkeyVirtualKeyKey = "HotkeyVirtualKey";
        private const string HotkeyDisplayKey = "HotkeyDisplay";

        internal static AppSettings Load()
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            EnsureSettingsVersion(values);

            var saveFolderPath = values.TryGetValue(SaveFolderPathKey, out var folderValue)
                ? folderValue as string
                : null;

            HotkeySettings? hotkey = null;
            if (values.TryGetValue(HotkeyModifiersKey, out var modifiersValue) &&
                values.TryGetValue(HotkeyVirtualKeyKey, out var virtualKeyValue) &&
                values.TryGetValue(HotkeyDisplayKey, out var displayValue) &&
                modifiersValue is int modifiersInt &&
                virtualKeyValue is int virtualKeyInt &&
                displayValue is string display &&
                !string.IsNullOrWhiteSpace(display) &&
                modifiersInt > 0 &&
                virtualKeyInt > 0)
            {
                hotkey = new HotkeySettings((uint)modifiersInt, (uint)virtualKeyInt, display);
            }

            return new AppSettings(saveFolderPath, hotkey);
        }

        internal static void SaveHotkey(uint modifiers, uint virtualKey, string display)
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            EnsureSettingsVersion(values);

            values[HotkeyModifiersKey] = (int)modifiers;
            values[HotkeyVirtualKeyKey] = (int)virtualKey;
            values[HotkeyDisplayKey] = display;
            SettingsChanged?.Invoke();
        }

        internal static void SaveFolderPath(string folderPath)
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            EnsureSettingsVersion(values);

            if (values.TryGetValue(SaveFolderPathKey, out var existingValue) &&
                existingValue is string existingPath &&
                string.Equals(existingPath, folderPath, StringComparison.Ordinal))
            {
                return;
            }

            values[SaveFolderPathKey] = folderPath;
            SaveFolderPathChanged?.Invoke();
            SettingsChanged?.Invoke();
        }

        internal static HotkeySettings GetDefaultHotkey()
        {
            return new HotkeySettings(DefaultHotkeyModifiers, DefaultHotkeyVirtualKey, DefaultHotkeyDisplay);
        }

        internal static bool TryGetEffectiveHotkey(out HotkeySettings hotkey)
        {
            var settings = Load();
            if (settings.Hotkey is not null && IsValidHotkey(settings.Hotkey))
            {
                hotkey = settings.Hotkey;
                return true;
            }

            var fallback = GetDefaultHotkey();
            if (IsValidHotkey(fallback))
            {
                hotkey = fallback;
                return true;
            }

            hotkey = new HotkeySettings(0, 0, string.Empty);
            return false;
        }

        internal static IReadOnlyList<GlobalSetupIssue> GetGlobalSetupIssues()
        {
            var issues = new List<GlobalSetupIssue>();
            var settings = Load();
            var saveFolderPath = !string.IsNullOrWhiteSpace(settings.SaveFolderPath)
                ? settings.SaveFolderPath
                : GetDefaultDesktopFolderPath();

            if (!TryValidateWritableFolder(saveFolderPath, out _))
            {
                issues.Add(new GlobalSetupIssue(
                    InfoBarSeverity.Error,
                    "Save location required",
                    "Choose a writable folder to enable screenshot features.",
                    "Open Save Location",
                    "save-location"));
            }

            if (!TryGetEffectiveHotkey(out _))
            {
                issues.Add(new GlobalSetupIssue(
                    InfoBarSeverity.Error,
                    "Hotkey required",
                    "Set a key-binding before using screenshot features.",
                    "Open Key-Bindings",
                    "keybindings"));
            }

            return issues;
        }

        internal static string GetDefaultDesktopFolderPath()
        {
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (string.IsNullOrWhiteSpace(desktopPath))
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }

            var screenshotsSubfolder = Path.Combine(desktopPath, DefaultScreenshotsFolderName);
            try
            {
                Directory.CreateDirectory(screenshotsSubfolder);
                return screenshotsSubfolder;
            }
            catch
            {
                return desktopPath;
            }
        }

        internal static bool TryValidateWritableFolder(string? folderPath, out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                errorMessage = "No folder selected.";
                return false;
            }

            if (!Directory.Exists(folderPath))
            {
                errorMessage = "Folder does not exist.";
                return false;
            }

            var probePath = Path.Combine(folderPath, $".helvety-write-check-{Guid.NewGuid():N}.tmp");
            try
            {
                using var stream = new FileStream(
                    probePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    1,
                    FileOptions.DeleteOnClose);
                stream.WriteByte(0);
            }
            catch (Exception ex)
            {
                errorMessage = $"Folder is not writable ({ex.Message})";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        private static void EnsureSettingsVersion(IPropertySet values)
        {
            if (values.TryGetValue(SettingsVersionKey, out var versionValue) &&
                versionValue is int version &&
                version >= CurrentSettingsVersion)
            {
                return;
            }

            values[SettingsVersionKey] = CurrentSettingsVersion;
        }

        private static bool IsValidHotkey(HotkeySettings hotkey)
        {
            return hotkey.Modifiers > 0 &&
                   hotkey.VirtualKey > 0 &&
                   !string.IsNullOrWhiteSpace(hotkey.Display);
        }
    }

    internal sealed record AppSettings(string? SaveFolderPath, HotkeySettings? Hotkey);

    internal sealed record HotkeySettings(uint Modifiers, uint VirtualKey, string Display);

    internal sealed record GlobalSetupIssue(
        InfoBarSeverity Severity,
        string Title,
        string Message,
        string ActionText,
        string RouteTag);
}
