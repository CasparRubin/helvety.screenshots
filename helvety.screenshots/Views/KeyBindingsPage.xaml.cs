using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;

namespace helvety.screenshots.Views
{
    public sealed partial class KeyBindingsPage : Page
    {
        private const int WhKeyboardLl = 13;
        private const uint WmKeydown = 0x0100;
        private const uint WmKeyup = 0x0101;
        private const uint WmSyskeydown = 0x0104;
        private const uint WmSyskeyup = 0x0105;
        private const uint ModAlt = 0x0001;
        private const uint ModControl = 0x0002;
        private const uint ModShift = 0x0004;
        private const uint ModWin = 0x0008;
        private const int ProbeHotkeyId = 40001;
        private const int VkEscape = 0x1B;
        private const int VkPrintScreen = 0x2C;
        private const int VkShift = 0x10;
        private const int VkControl = 0x11;
        private const int VkMenu = 0x12;
        private const int VkLwin = 0x5B;
        private const int VkRwin = 0x5C;
        private const string DefaultCaptureInstructionText = "Set a shortcut with at least one modifier.";
        private const string CaptureBlockedInstructionText = "Choose a save location first.";

        private readonly ObservableCollection<string> _messages = new();
        private HotkeyBinding? _currentBinding;
        private nint _keyboardHookHandle;
        private KeyboardHookProc? _keyboardHookProc;
        private bool _isKeyboardHookInstalled;
        private bool _isTriggerKeyDown;
        private bool _isCaptureMode;
        private uint _captureModifiers;
        private uint? _captureTriggerKey;
        private string? _captureTriggerName;
        private HotkeyBinding _startupBinding;
        private string _saveFolderPath = string.Empty;
        private bool _loadedHotkeyFromSettings;
        private bool _hasValidSaveFolder;

        public KeyBindingsPage()
        {
            InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Required;

            MessageListView.ItemsSource = _messages;
            _startupBinding = GetDefaultHotkeyBinding();
            InitializeSettings();
            InitializeHotkeyInfrastructure();
            RegisterInitialBinding();

            SettingsService.SaveFolderPathChanged += SettingsService_SaveFolderPathChanged;
            if (App.MainAppWindow is not null)
            {
                App.MainAppWindow.Closed += MainWindow_Closed;
            }

            Unloaded += KeyBindingsPage_Unloaded;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            RefreshSaveFolderState();
        }

        private void SettingsService_SaveFolderPathChanged()
        {
            DispatcherQueue.TryEnqueue(RefreshSaveFolderState);
        }

        private void KeyBindingsPage_Unloaded(object sender, RoutedEventArgs e)
        {
            SettingsService.SaveFolderPathChanged -= SettingsService_SaveFolderPathChanged;
            if (App.MainAppWindow is not null)
            {
                App.MainAppWindow.Closed -= MainWindow_Closed;
            }

            Unloaded -= KeyBindingsPage_Unloaded;
        }

        private void InitializeSettings()
        {
            var settings = SettingsService.Load();
            _saveFolderPath = !string.IsNullOrWhiteSpace(settings.SaveFolderPath)
                ? settings.SaveFolderPath
                : SettingsService.GetDefaultDesktopFolderPath();

            if (settings.Hotkey is not null)
            {
                _startupBinding = new HotkeyBinding(
                    settings.Hotkey.Modifiers,
                    settings.Hotkey.VirtualKey,
                    settings.Hotkey.Display);
                _loadedHotkeyFromSettings = true;
            }
            else
            {
                _startupBinding = GetDefaultHotkeyBinding();
                _loadedHotkeyFromSettings = false;
            }

            RefreshSaveFolderState();
        }

        private static HotkeyBinding GetDefaultHotkeyBinding()
        {
            var fallback = SettingsService.GetDefaultHotkey();
            return new HotkeyBinding(fallback.Modifiers, fallback.VirtualKey, fallback.Display);
        }

        private void RefreshSaveFolderState()
        {
            var settings = SettingsService.Load();
            _saveFolderPath = !string.IsNullOrWhiteSpace(settings.SaveFolderPath)
                ? settings.SaveFolderPath
                : SettingsService.GetDefaultDesktopFolderPath();

            if (SettingsService.TryValidateWritableFolder(_saveFolderPath, out var validationError))
            {
                _hasValidSaveFolder = true;
                if (BindingStatusText.Text.StartsWith("Save location needed", StringComparison.Ordinal))
                {
                    BindingStatusText.Text = string.Empty;
                }
            }
            else
            {
                _hasValidSaveFolder = false;
                BindingStatusText.Text = $"Save location needed: {validationError}";
            }

            UpdateFeatureAvailability();
        }

        private void UpdateFeatureAvailability()
        {
            if (_isCaptureMode)
            {
                return;
            }

            StartCaptureButton.IsEnabled = _hasValidSaveFolder;
            CaptureInstructionText.Text = _hasValidSaveFolder
                ? DefaultCaptureInstructionText
                : CaptureBlockedInstructionText;
        }

        private void InitializeHotkeyInfrastructure()
        {
            _keyboardHookProc = KeyboardHookCallback;
            _keyboardHookHandle = SetWindowsHookEx(WhKeyboardLl, _keyboardHookProc, nint.Zero, 0);
            _isKeyboardHookInstalled = _keyboardHookHandle != nint.Zero;
        }

        private void RegisterInitialBinding()
        {
            if (TryApplyBinding(_startupBinding, out var statusMessage))
            {
                BindingStatusText.Text = _loadedHotkeyFromSettings
                    ? $"Using {_startupBinding.Display}"
                    : $"Default: {_startupBinding.Display}";
                return;
            }

            BindingStatusText.Text = statusMessage;
            AddMessage(statusMessage);
        }

        private void StartCaptureButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_hasValidSaveFolder)
            {
                const string blockedMessage = "Set a save location before changing hotkeys.";
                BindingStatusText.Text = blockedMessage;
                AddMessage(blockedMessage);
                return;
            }

            BeginCaptureMode();
        }

        private bool TryApplyBinding(HotkeyBinding requestedBinding, out string statusMessage)
        {
            if (!_isKeyboardHookInstalled)
            {
                var hookError = Marshal.GetLastWin32Error();
                statusMessage = $"Keyboard hook failed to install (error {hookError}).";
                return false;
            }

            _currentBinding = requestedBinding;
            _isTriggerKeyDown = false;
            CurrentBindingText.Text = $"Current Binding: {requestedBinding.Display}";
            SettingsService.SaveHotkey(requestedBinding.Modifiers, requestedBinding.VirtualKey, requestedBinding.Display);
            if (IsLikelyClaimedByAnotherHotkey(requestedBinding))
            {
                statusMessage = $"Binding applied: {requestedBinding.Display}. Note: this combo appears to be claimed/reserved by another app or Windows, but this app still listens with its keyboard hook.";
            }
            else
            {
                statusMessage = $"Binding applied: {requestedBinding.Display}";
            }

            return true;
        }

        private void BeginCaptureMode()
        {
            _isCaptureMode = true;
            _captureModifiers = 0;
            _captureTriggerKey = null;
            _captureTriggerName = null;
            _isTriggerKeyDown = false;

            StartCaptureButton.IsEnabled = false;
            StartCaptureButton.Content = "Listening...";
            CaptureInstructionText.Text = "Press modifiers + key. Release key to save. Esc to cancel.";
            CapturePreviewText.Text = "Capture preview: waiting for input...";
        }

        private void CancelCaptureMode(string reason)
        {
            _isCaptureMode = false;
            _captureModifiers = 0;
            _captureTriggerKey = null;
            _captureTriggerName = null;

            StartCaptureButton.IsEnabled = true;
            StartCaptureButton.Content = "Set Hotkey";
            UpdateFeatureAvailability();
            CapturePreviewText.Text = "Capture preview: (not listening)";
            BindingStatusText.Text = reason;
            AddMessage(reason);
        }

        private void FinalizeCaptureMode()
        {
            if (!_captureTriggerKey.HasValue || string.IsNullOrWhiteSpace(_captureTriggerName))
            {
                CancelCaptureMode("Could not capture a trigger key. Please try again.");
                return;
            }

            if (_captureModifiers == 0)
            {
                CancelCaptureMode("Hotkey must include at least one modifier (Ctrl, Alt, Shift, or Win).");
                return;
            }

            var display = BuildBindingDisplay(_captureModifiers, _captureTriggerName);
            var binding = new HotkeyBinding(_captureModifiers, _captureTriggerKey.Value, display);
            _isCaptureMode = false;

            StartCaptureButton.Content = "Set Hotkey";
            UpdateFeatureAvailability();
            CapturePreviewText.Text = $"Capture preview: {display}";

            if (TryApplyBinding(binding, out var statusMessage))
            {
                BindingStatusText.Text = statusMessage;
                AddMessage(statusMessage);
                return;
            }

            BindingStatusText.Text = statusMessage;
            AddMessage(statusMessage);
        }

        private static bool IsLikelyClaimedByAnotherHotkey(HotkeyBinding binding)
        {
            if (RegisterHotKey(nint.Zero, ProbeHotkeyId, binding.Modifiers, binding.VirtualKey))
            {
                UnregisterHotKey(nint.Zero, ProbeHotkeyId);
                return false;
            }

            return true;
        }

        private static bool IsModifierKey(uint virtualKey)
        {
            return virtualKey is VkShift or VkControl or VkMenu or VkLwin or VkRwin;
        }

        private void HandleCaptureKeyEvent(uint message, uint virtualKey)
        {
            if (message is WmKeydown or WmSyskeydown)
            {
                if (virtualKey == VkEscape)
                {
                    DispatcherQueue.TryEnqueue(() => CancelCaptureMode("Capture canceled."));
                    return;
                }

                _captureModifiers = GetCurrentModifiers();
                if (!IsModifierKey(virtualKey))
                {
                    _captureTriggerKey = virtualKey;
                    _captureTriggerName = GetKeyDisplayName(virtualKey);
                }

                DispatcherQueue.TryEnqueue(UpdateCapturePreview);
                return;
            }

            if (message is WmKeyup or WmSyskeyup)
            {
                if (_captureTriggerKey.HasValue && _captureTriggerKey.Value == virtualKey)
                {
                    DispatcherQueue.TryEnqueue(FinalizeCaptureMode);
                    return;
                }

                _captureModifiers = GetCurrentModifiers();
                DispatcherQueue.TryEnqueue(UpdateCapturePreview);
            }
        }

        private void UpdateCapturePreview()
        {
            if (!_captureTriggerKey.HasValue || string.IsNullOrWhiteSpace(_captureTriggerName))
            {
                var modifiersOnly = BuildModifierPreview(_captureModifiers);
                CapturePreviewText.Text = string.IsNullOrEmpty(modifiersOnly)
                    ? "Capture preview: waiting for input..."
                    : $"Capture preview: {modifiersOnly}+...";
                return;
            }

            CapturePreviewText.Text = $"Capture preview: {BuildBindingDisplay(_captureModifiers, _captureTriggerName)}";
        }

        private static string BuildModifierPreview(uint modifiers)
        {
            var parts = new Collection<string>();

            if ((modifiers & ModControl) != 0)
            {
                parts.Add("Ctrl");
            }

            if ((modifiers & ModAlt) != 0)
            {
                parts.Add("Alt");
            }

            if ((modifiers & ModShift) != 0)
            {
                parts.Add("Shift");
            }

            if ((modifiers & ModWin) != 0)
            {
                parts.Add("Win");
            }

            return string.Join('+', parts);
        }

        private static string BuildBindingDisplay(uint modifiers, string keyName)
        {
            var modifiersPart = BuildModifierPreview(modifiers);
            return string.IsNullOrEmpty(modifiersPart) ? keyName : $"{modifiersPart}+{keyName}";
        }

        private static uint GetCurrentModifiers()
        {
            uint modifiers = 0;

            if (IsKeyDown(VkControl))
            {
                modifiers |= ModControl;
            }

            if (IsKeyDown(VkMenu))
            {
                modifiers |= ModAlt;
            }

            if (IsKeyDown(VkShift))
            {
                modifiers |= ModShift;
            }

            if (IsKeyDown(VkLwin) || IsKeyDown(VkRwin))
            {
                modifiers |= ModWin;
            }

            return modifiers;
        }

        private static string GetKeyDisplayName(uint virtualKey)
        {
            if (virtualKey >= 0x41 && virtualKey <= 0x5A)
            {
                return ((char)virtualKey).ToString();
            }

            if (virtualKey >= 0x30 && virtualKey <= 0x39)
            {
                return ((char)virtualKey).ToString();
            }

            if (virtualKey >= 0x70 && virtualKey <= 0x87)
            {
                return $"F{virtualKey - 0x6F}";
            }

            return virtualKey switch
            {
                0x20 => "Space",
                0x25 => "LeftArrow",
                0x26 => "UpArrow",
                0x27 => "RightArrow",
                0x28 => "DownArrow",
                VkPrintScreen => "PrintScreen",
                0x2D => "Insert",
                0x2E => "Delete",
                0x24 => "Home",
                0x23 => "End",
                0x21 => "PageUp",
                0x22 => "PageDown",
                0x08 => "Backspace",
                0x09 => "Tab",
                0x0D => "Enter",
                _ => $"VK_{virtualKey:X2}"
            };
        }

        private nint KeyboardHookCallback(int nCode, nuint wParam, nint lParam)
        {
            if (nCode >= 0)
            {
                var message = (uint)wParam;
                var keyData = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);

                if (_isCaptureMode)
                {
                    HandleCaptureKeyEvent(message, keyData.VkCode);
                }
                else if (_currentBinding is not null && _hasValidSaveFolder)
                {
                    var currentBinding = _currentBinding.Value;

                    if (message is WmKeydown or WmSyskeydown)
                    {
                        if (keyData.VkCode == currentBinding.VirtualKey &&
                            ModifiersMatch(currentBinding.Modifiers) &&
                            !_isTriggerKeyDown)
                        {
                            _isTriggerKeyDown = true;
                            DispatcherQueue.TryEnqueue(OnHotkeyPressed);
                        }
                    }
                    else if (message is WmKeyup or WmSyskeyup)
                    {
                        if (keyData.VkCode == currentBinding.VirtualKey)
                        {
                            _isTriggerKeyDown = false;
                        }
                    }
                }
            }

            return CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
        }

        private static bool ModifiersMatch(uint requiredModifiers)
        {
            var currentModifiers = GetCurrentModifiers();
            var ctrlDown = (currentModifiers & ModControl) != 0;
            var altDown = (currentModifiers & ModAlt) != 0;
            var shiftDown = (currentModifiers & ModShift) != 0;
            var winDown = (currentModifiers & ModWin) != 0;

            var requiresCtrl = (requiredModifiers & ModControl) != 0;
            var requiresAlt = (requiredModifiers & ModAlt) != 0;
            var requiresShift = (requiredModifiers & ModShift) != 0;
            var requiresWin = (requiredModifiers & ModWin) != 0;

            return ctrlDown == requiresCtrl &&
                   altDown == requiresAlt &&
                   shiftDown == requiresShift &&
                   winDown == requiresWin;
        }

        private static bool IsKeyDown(int virtualKey)
        {
            return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
        }

        private void OnHotkeyPressed()
        {
            var binding = _currentBinding?.Display ?? "Unknown";
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            AddMessage($"Hotkey {binding} pressed at {timestamp}");
        }

        private void AddMessage(string message)
        {
            _messages.Insert(0, message);
            if (_messages.Count > 100)
            {
                _messages.RemoveAt(_messages.Count - 1);
            }
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            if (_isKeyboardHookInstalled)
            {
                UnhookWindowsHookEx(_keyboardHookHandle);
                _isKeyboardHookInstalled = false;
            }
        }

        private readonly record struct HotkeyBinding(uint Modifiers, uint VirtualKey, string Display);

        private delegate nint KeyboardHookProc(int nCode, nuint wParam, nint lParam);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(nint hhk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern nint SetWindowsHookEx(int idHook, KeyboardHookProc lpfn, nint hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        private static extern nint CallNextHookEx(nint hhk, int nCode, nuint wParam, nint lParam);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnregisterHotKey(nint hWnd, int id);

        [StructLayout(LayoutKind.Sequential)]
        private struct KbdLlHookStruct
        {
            public uint VkCode;
            public uint ScanCode;
            public uint Flags;
            public uint Time;
            public nuint DwExtraInfo;
        }
    }
}
