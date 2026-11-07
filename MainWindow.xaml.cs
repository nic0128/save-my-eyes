using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using save_my_eyes.Services;
using save_my_eyes.ViewModels;
using save_my_eyes.Views;
using Windows.Graphics;
using Windows.UI;
using Windows.UI.ViewManagement;
using WinRT.Interop;
using Colors = Microsoft.UI.Colors;

namespace save_my_eyes
{
    public sealed partial class MainWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);

        private const int MinimumWidth = 440;
        private const int MinimumHeight = 620;

        private readonly AlertService _alerts;
        private readonly TrayService _tray;
        private readonly WindowMinSize _minSize;
        private bool _dialogOpen;

        public MainWindowViewModel ViewModel { get; }

        public MainWindow()
        {
            ViewModel = new MainWindowViewModel(DispatcherQueue, new SettingsService());

            InitializeComponent();

            RootGrid.Background = (Brush)RootGrid.Resources["AppBackgroundBrush"];

            ConfigureTitleBar();

            _minSize = new WindowMinSize(this, MinimumWidth, MinimumHeight);

            _alerts = new AlertService(this);

            _tray = new TrayService(this)
            {
                ShouldHideOnMinimize = () => !ViewModel.Settings.KeepInTaskbar,
                ToggleLabel = ViewModel.ToggleLabel
            };
            _tray.ToggleRequested += (_, _) => ViewModel.Toggle();
            _tray.ExitRequested += (_, _) => DispatcherQueue.TryEnqueue(Close);

            ConfigureWindow();

            ViewModel.PropertyChanged += OnViewModelPropertyChanged;
            ViewModel.BreakStarted += OnBreakStarted;
            ViewModel.BreakCompleted += OnBreakCompleted;
            ViewModel.SettingsChanged += OnSettingsChanged;
            Closed += OnClosed;

            RootGrid.Loaded += (_, _) => ApplyPalette();
            RootGrid.ActualThemeChanged += (_, _) => ApplyPalette();
            ApplyPalette();

            Title = ViewModel.WindowTitle;
        }

        private void ConfigureTitleBar()
        {
            try
            {
                ExtendsContentIntoTitleBar = true;
                SetTitleBar(TitleBarDragArea);

                if (AppWindowTitleBar.IsCustomizationSupported())
                {
                    AppWindowTitleBar titleBar = AppWindow.TitleBar;
                    titleBar.ButtonBackgroundColor = Colors.Transparent;
                    titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                }
            }
            catch (Exception)
            {
            }
        }

        private void ConfigureWindow()
        {
            try
            {
                IntPtr hwnd = WindowNative.GetWindowHandle(this);
                uint dpi = GetDpiForWindow(hwnd);
                double scale = dpi == 0 ? 1.0 : dpi / 96.0;

                AppWindow.Resize(new SizeInt32((int)(MinimumWidth * scale), (int)(MinimumHeight * scale)));

                RectInt32 workArea = DisplayArea
                    .GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest)
                    .WorkArea;
                SizeInt32 size = AppWindow.Size;

                AppWindow.Move(new PointInt32(
                    workArea.X + (workArea.Width - size.Width) / 2,
                    workArea.Y + (workArea.Height - size.Height) / 2));

                string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
                if (File.Exists(iconPath))
                {
                    AppWindow.SetIcon(iconPath);
                }
            }
            catch (Exception)
            {
            }
        }

        private void ApplyPalette()
        {
            AppSettings s = ViewModel.Settings;

            RootGrid.RequestedTheme = s.ThemeMode switch
            {
                1 => ElementTheme.Light,
                2 => ElementTheme.Dark,
                _ => ElementTheme.Default
            };

            var ui = new UISettings();

            bool dark = s.ThemeMode switch
            {
                1 => false,
                2 => true,
                _ => !ColorUtil.IsLight(ui.GetColorValue(UIColorType.Background))
            };

            Color accent = ColorUtil.TryParse(s.AccentColor, out Color a)
                ? a
                : ui.GetColorValue(dark ? UIColorType.AccentLight2 : UIColorType.AccentDark1);

            Color text = ColorUtil.TryParse(s.TextColor, out Color t)
                ? t
                : (dark ? Color.FromArgb(255, 255, 255, 255) : Color.FromArgb(228, 0, 0, 0));

            Color background = ColorUtil.TryParse(s.BackgroundColor, out Color b)
                ? b
                : Color.FromArgb(0, 0, 0, 0);

            Color card = ColorUtil.TryParse(s.CardColor, out Color c)
                ? c
                : (dark ? Color.FromArgb(13, 255, 255, 255) : Color.FromArgb(179, 255, 255, 255));

            Color breakColor = ColorUtil.TryParse(s.BreakColor, out Color br) ? br : accent;

            Color onAccent = ColorUtil.IsLight(accent)
                ? Color.FromArgb(255, 0, 0, 0)
                : Color.FromArgb(255, 255, 255, 255);

            SetBrush("AppBackgroundBrush", background);
            SetBrush("CardBrush", card);
            SetBrush("PrimaryTextBrush", text);
            SetBrush("SecondaryTextBrush", ColorUtil.WithAlpha(text, 180));
            SetBrush("AccentBrush", accent);
            SetBrush("BreakBrush", breakColor);

            SetBrush("AccentButtonBackground", accent);
            SetBrush("AccentButtonBackgroundPointerOver", ColorUtil.WithAlpha(accent, 230));
            SetBrush("AccentButtonBackgroundPressed", ColorUtil.WithAlpha(accent, 204));
            SetBrush("AccentButtonForeground", onAccent);
            SetBrush("AccentButtonForegroundPointerOver", onAccent);
            SetBrush("AccentButtonForegroundPressed", onAccent);

            SetBrush("ButtonForeground", text);
            SetBrush("ButtonForegroundPointerOver", text);
            SetBrush("ButtonForegroundPressed", text);

            SetBrush("HyperlinkButtonForeground", accent);
            SetBrush("HyperlinkButtonForegroundPointerOver", ColorUtil.WithAlpha(accent, 230));
            SetBrush("HyperlinkButtonForegroundPressed", ColorUtil.WithAlpha(accent, 204));

            try
            {
                if (AppWindowTitleBar.IsCustomizationSupported())
                {
                    AppWindowTitleBar titleBar = AppWindow.TitleBar;
                    titleBar.ButtonForegroundColor = text;
                    titleBar.ButtonHoverForegroundColor = text;
                    titleBar.ButtonPressedForegroundColor = text;
                    titleBar.ButtonInactiveForegroundColor = ColorUtil.WithAlpha(text, 140);
                    titleBar.ButtonHoverBackgroundColor = ColorUtil.WithAlpha(text, 26);
                    titleBar.ButtonPressedBackgroundColor = ColorUtil.WithAlpha(text, 15);
                }
            }
            catch (Exception)
            {
            }
        }

        private void SetBrush(string key, Color color)
        {
            if (RootGrid.Resources[key] is SolidColorBrush brush)
            {
                brush.Color = color;
            }
        }

        private void UpdateBreakVisuals()
        {
            string key = ViewModel.IsBreaking ? "BreakBrush" : "PrimaryTextBrush";

            if (RootGrid.Resources[key] is Brush brush)
            {
                TimerText.Foreground = brush;
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(MainWindowViewModel.WindowTitle):
                    Title = ViewModel.WindowTitle;
                    _tray.UpdateTooltip(ViewModel.WindowTitle);
                    break;

                case nameof(MainWindowViewModel.ToggleLabel):
                    _tray.ToggleLabel = ViewModel.ToggleLabel;
                    break;

                case nameof(MainWindowViewModel.IsBreaking):
                    UpdateBreakVisuals();
                    if (!ViewModel.IsBreaking)
                    {
                        _alerts.ReleaseTopmost();
                    }
                    break;
            }
        }

        private void OnBreakStarted(object? sender, EventArgs e)
        {
            _alerts.NotifyBreakStarted(ViewModel.Settings);
        }

        private void OnBreakCompleted(object? sender, EventArgs e)
        {
            _alerts.NotifyBreakCompleted(ViewModel.Settings);

            if (_alerts.ConsumeMinimizeAfterBreak())
            {
                MinimizeAgain();
            }
        }

        private void MinimizeAgain()
        {
            try
            {
                if (!ViewModel.Settings.KeepInTaskbar)
                {
                    _tray.HideToTray();
                }
                else if (AppWindow.Presenter is OverlappedPresenter presenter)
                {
                    presenter.Minimize();
                }
            }
            catch (Exception)
            {
            }
        }

        private void OnSettingsChanged(object? sender, EventArgs e)
        {
            ApplyPalette();
        }

        private void OnClosed(object sender, WindowEventArgs args)
        {
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            ViewModel.BreakStarted -= OnBreakStarted;
            ViewModel.BreakCompleted -= OnBreakCompleted;
            ViewModel.SettingsChanged -= OnSettingsChanged;

            _minSize.Dispose();
            _tray.Dispose();
            _alerts.Dispose();
            ViewModel.Shutdown();
        }

        private async void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_dialogOpen)
            {
                return;
            }

            _dialogOpen = true;
            try
            {
                var dialog = new SettingsDialog(this, _alerts, ViewModel.Settings);

                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    ViewModel.ApplySettings(dialog.BuildResult());

                    if (dialog.ResetBreaksRequested)
                    {
                        ViewModel.ResetBreaks();
                    }
                }
            }
            finally
            {
                _dialogOpen = false;
            }
        }
    }
}
