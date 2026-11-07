using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using save_my_eyes.Services;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using Windows.UI.ViewManagement;
using WinRT.Interop;
using Colors = Microsoft.UI.Colors;

namespace save_my_eyes.Views
{
    public sealed class SettingsDialog : ContentDialog
    {
        private const double DialogChromeHeight = 250;

        private readonly Window _owner;
        private readonly AlertService _alerts;
        private readonly AppSettings _original;

        private readonly ScrollViewer _scroll;
        private readonly NumberBox _workMinutes;
        private readonly NumberBox _workSeconds;
        private readonly NumberBox _breakMinutes;
        private readonly NumberBox _breakSeconds;
        private readonly ToggleSwitch _playSounds;
        private readonly ToggleSwitch _bringToFront;
        private readonly ToggleSwitch _keepInTaskbar;
        private readonly ToggleSwitch _showBreakCount;
        private readonly ComboBox _theme;
        private readonly SoundRow _startSound;
        private readonly SoundRow _endSound;
        private readonly ColorRow _accent;
        private readonly ColorRow _background;
        private readonly ColorRow _card;
        private readonly ColorRow _text;
        private readonly ColorRow _break;

        public SettingsDialog(Window owner, AlertService alerts, AppSettings current)
        {
            _owner = owner;
            _alerts = alerts;
            _original = current;

            XamlRoot = owner.Content.XamlRoot;

            if (owner.Content is FrameworkElement root)
            {
                RequestedTheme = root.RequestedTheme;
            }

            Title = "Settings";
            PrimaryButtonText = "Save";
            CloseButtonText = "Cancel";
            DefaultButton = ContentDialogButton.Primary;

            _workMinutes = MakeNumberBox("Work interval (minutes)", current.WorkSeconds / 60, 0, 120, 1, 5);
            _workSeconds = MakeNumberBox("Work interval (seconds)", current.WorkSeconds % 60, 0, 59, 1, 10);
            _breakMinutes = MakeNumberBox("Break length (minutes)", current.BreakSeconds / 60, 0, 60, 1, 5);
            _breakSeconds = MakeNumberBox("Break length (seconds)", current.BreakSeconds % 60, 0, 59, 1, 10);

            var workRow = new Grid { ColumnSpacing = 12 };
            workRow.ColumnDefinitions.Add(new ColumnDefinition());
            workRow.ColumnDefinitions.Add(new ColumnDefinition());
            Grid.SetColumn(_workSeconds, 1);
            workRow.Children.Add(_workMinutes);
            workRow.Children.Add(_workSeconds);

            var breakRow = new Grid { ColumnSpacing = 12 };
            breakRow.ColumnDefinitions.Add(new ColumnDefinition());
            breakRow.ColumnDefinitions.Add(new ColumnDefinition());
            Grid.SetColumn(_breakSeconds, 1);
            breakRow.Children.Add(_breakMinutes);
            breakRow.Children.Add(_breakSeconds);

            var resetTimer = new HyperlinkButton
            {
                Content = "Reset to default",
                Padding = new Thickness(0)
            };
            resetTimer.Click += (_, _) =>
            {
                var defaults = new AppSettings();
                _workMinutes.Value = defaults.WorkSeconds / 60;
                _workSeconds.Value = defaults.WorkSeconds % 60;
                _breakMinutes.Value = defaults.BreakSeconds / 60;
                _breakSeconds.Value = defaults.BreakSeconds % 60;
            };

            _playSounds = new ToggleSwitch
            {
                Header = "Play a sound when a break starts and ends",
                IsOn = current.PlaySounds
            };

            _startSound = new SoundRow(this, "Break start sound", current.StartSoundPath, isStartSound: true);
            _endSound = new SoundRow(this, "Break end sound", current.EndSoundPath, isStartSound: false);

            _bringToFront = new ToggleSwitch
            {
                Header = "Bring this window to the front when a break starts",
                IsOn = current.BringToFront
            };

            _keepInTaskbar = new ToggleSwitch
            {
                Header = "Keep in the taskbar when minimized",
                IsOn = current.KeepInTaskbar
            };

            _showBreakCount = new ToggleSwitch
            {
                Header = "Show \"breaks taken today\" in the window",
                IsOn = current.ShowBreakCount
            };

            var resetBreaks = new HyperlinkButton
            {
                Content = "Reset today's break count",
                Padding = new Thickness(0)
            };
            resetBreaks.Click += (_, _) =>
            {
                ResetBreaksRequested = true;
                resetBreaks.Content = "Break count will reset when you save";
                resetBreaks.IsEnabled = false;
            };

            _theme = new ComboBox { Header = "Theme", MinWidth = 200 };
            _theme.Items.Add("Windows default");
            _theme.Items.Add("Light");
            _theme.Items.Add("Dark");
            _theme.SelectedIndex = Math.Clamp(current.ThemeMode, 0, 2);

            Color systemAccent = new UISettings().GetColorValue(UIColorType.Accent);

            _accent = new ColorRow("Accent", current.AccentColor, systemAccent);
            _background = new ColorRow("Background", current.BackgroundColor, Colors.Transparent);
            _card = new ColorRow("Panel", current.CardColor, Colors.Transparent);
            _text = new ColorRow("Text", current.TextColor, Colors.Gray);
            _break = new ColorRow("Break color", current.BreakColor, systemAccent);

            var resetColors = new HyperlinkButton
            {
                Content = "Reset all colors to default",
                Padding = new Thickness(0)
            };
            resetColors.Click += (_, _) =>
            {
                _accent.SetAutomatic();
                _background.SetAutomatic();
                _card.SetAutomatic();
                _text.SetAutomatic();
                _break.SetAutomatic();
            };

            var panel = new StackPanel { Spacing = 14 };

            panel.Children.Add(Section("Timer", first: true));
            panel.Children.Add(workRow);
            panel.Children.Add(breakRow);
            panel.Children.Add(resetTimer);
            panel.Children.Add(Note("Changes to the intervals apply to the next phase if a session is running."));

            panel.Children.Add(Section("Alerts"));
            panel.Children.Add(_playSounds);
            panel.Children.Add(_startSound.Root);
            panel.Children.Add(_endSound.Root);

            panel.Children.Add(Section("Window"));
            panel.Children.Add(_keepInTaskbar);
            panel.Children.Add(Note("When off, minimizing hides the app in the system tray (hidden icons)."));
            panel.Children.Add(_bringToFront);

            panel.Children.Add(Section("Break counter"));
            panel.Children.Add(_showBreakCount);
            panel.Children.Add(resetBreaks);

            panel.Children.Add(Section("Appearance"));
            panel.Children.Add(_theme);
            panel.Children.Add(_accent.Root);
            panel.Children.Add(_background.Root);
            panel.Children.Add(_card.Root);
            panel.Children.Add(_text.Root);
            panel.Children.Add(_break.Root);
            panel.Children.Add(resetColors);

            _scroll = new ScrollViewer
            {
                Content = panel,
                Padding = new Thickness(0, 0, 16, 0),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                IsTabStop = true
            };
            Content = _scroll;

            UpdateScrollHeight();
            _owner.SizeChanged += OnOwnerSizeChanged;

            Opened += (_, _) => _owner.DispatcherQueue.TryEnqueue(() => _scroll.Focus(FocusState.Programmatic));

            Closed += (_, _) =>
            {
                _owner.SizeChanged -= OnOwnerSizeChanged;
                _alerts.StopPreview();
                _startSound.Detach();
                _endSound.Detach();
            };
        }

        public bool ResetBreaksRequested { get; private set; }

        public AppSettings BuildResult()
        {
            var result = new AppSettings();
            result.CopyPreferencesFrom(_original);

            int minutes = ReadInt(_workMinutes, _original.WorkSeconds / 60);
            int seconds = ReadInt(_workSeconds, _original.WorkSeconds % 60);
            result.WorkSeconds = minutes * 60 + seconds;
            int breakMinutes = ReadInt(_breakMinutes, _original.BreakSeconds / 60);
            int breakSeconds = ReadInt(_breakSeconds, _original.BreakSeconds % 60);
            result.BreakSeconds = breakMinutes * 60 + breakSeconds;

            result.PlaySounds = _playSounds.IsOn;
            result.StartSoundPath = _startSound.FilePath;
            result.EndSoundPath = _endSound.FilePath;
            result.BringToFront = _bringToFront.IsOn;
            result.KeepInTaskbar = _keepInTaskbar.IsOn;
            result.ShowBreakCount = _showBreakCount.IsOn;
            result.ThemeMode = Math.Max(0, _theme.SelectedIndex);

            result.AccentColor = _accent.Hex;
            result.BackgroundColor = _background.Hex;
            result.CardColor = _card.Hex;
            result.TextColor = _text.Hex;
            result.BreakColor = _break.Hex;

            return result;
        }

        private void OnOwnerSizeChanged(object sender, WindowSizeChangedEventArgs args)
        {
            UpdateScrollHeight();
        }

        private void UpdateScrollHeight()
        {
            _scroll.MaxHeight = Math.Max(160, _owner.Bounds.Height - DialogChromeHeight);
        }

        private static NumberBox MakeNumberBox(
            string header, double value, double min, double max, double small, double large)
        {
            return new NumberBox
            {
                Header = header,
                Value = value,
                Minimum = min,
                Maximum = max,
                SmallChange = small,
                LargeChange = large,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact
            };
        }

        private static int ReadInt(NumberBox box, int fallback) =>
            double.IsNaN(box.Value) ? fallback : (int)Math.Round(box.Value);

        private static TextBlock Section(string text, bool first = false) => new TextBlock
        {
            Text = text,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, first ? 0 : 10, 0, 0)
        };

        private static TextBlock Note(string text) => new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.7
        };

        private async Task<string?> PickSoundFileAsync()
        {
            var picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.MusicLibrary,
                ViewMode = PickerViewMode.List
            };

            foreach (string extension in new[] { ".wav", ".mp3", ".wma", ".m4a", ".aac", ".flac" })
            {
                picker.FileTypeFilter.Add(extension);
            }

            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(_owner));

            StorageFile? file = await picker.PickSingleFileAsync();
            return file?.Path;
        }

        private sealed class SoundRow
        {
            private const string PlayGlyph = "\uE768";
            private const string StopGlyph = "\uE71A";
            private const double ControlHeight = 32;

            private readonly SettingsDialog _dialog;
            private readonly bool _isStartSound;
            private readonly TextBlock _name;
            private readonly FontIcon _previewIcon;
            private readonly Button _previewButton;
            private readonly HyperlinkButton _resetLink;
            private bool _isPlaying;

            public string FilePath { get; private set; }

            public FrameworkElement Root { get; }

            public SoundRow(SettingsDialog dialog, string label, string filePath, bool isStartSound)
            {
                _dialog = dialog;
                _isStartSound = isStartSound;
                FilePath = filePath ?? string.Empty;

                _name = new TextBlock
                {
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Opacity = 0.7
                };

                var header = new Grid { ColumnSpacing = 12 };
                header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                header.ColumnDefinitions.Add(new ColumnDefinition());
                var labelText = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(_name, 1);
                header.Children.Add(labelText);
                header.Children.Add(_name);

                var browse = new Button { Content = "Browse...", Height = ControlHeight };
                browse.Click += async (_, _) =>
                {
                    string? picked = await _dialog.PickSoundFileAsync();
                    if (!string.IsNullOrEmpty(picked))
                    {
                        FilePath = picked;
                        Refresh();
                    }
                };

                _previewIcon = new FontIcon { Glyph = PlayGlyph, FontSize = 14 };
                _previewButton = new Button
                {
                    Content = _previewIcon,
                    Height = ControlHeight,
                    Width = 44,
                    Padding = new Thickness(0)
                };
                ToolTipService.SetToolTip(_previewButton, "Preview");
                _previewButton.Click += OnPreviewClicked;

                _resetLink = new HyperlinkButton
                {
                    Content = "Reset to default",
                    VerticalAlignment = VerticalAlignment.Center,
                    Padding = new Thickness(4, 0, 4, 0)
                };
                _resetLink.Click += (_, _) =>
                {
                    if (_isPlaying)
                    {
                        _dialog._alerts.StopPreview();
                    }

                    FilePath = string.Empty;
                    Refresh();
                };

                var line = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    VerticalAlignment = VerticalAlignment.Center
                };
                line.Children.Add(browse);
                line.Children.Add(_previewButton);
                line.Children.Add(_resetLink);

                var root = new StackPanel { Spacing = 6 };
                root.Children.Add(header);
                root.Children.Add(line);
                Root = root;

                _dialog._alerts.PreviewEnded += OnPreviewEnded;
                Refresh();
            }

            public void Detach()
            {
                _dialog._alerts.PreviewEnded -= OnPreviewEnded;
            }

            private void OnPreviewClicked(object sender, RoutedEventArgs e)
            {
                if (_isPlaying)
                {
                    _dialog._alerts.StopPreview();
                    return;
                }

                _dialog._alerts.StartPreview(FilePath, _isStartSound);
                SetPlaying(_dialog._alerts.IsPreviewing);
            }

            private void OnPreviewEnded(object? sender, EventArgs e)
            {
                SetPlaying(false);
            }

            private void SetPlaying(bool playing)
            {
                _isPlaying = playing;
                _previewIcon.Glyph = playing ? StopGlyph : PlayGlyph;
                ToolTipService.SetToolTip(_previewButton, playing ? "Stop" : "Preview");
            }

            private void Refresh()
            {
                bool hasCustom = !string.IsNullOrEmpty(FilePath);

                _name.Text = hasCustom ? System.IO.Path.GetFileName(FilePath) : "Default sound";
                _resetLink.IsEnabled = hasCustom;
            }
        }

        private sealed class ColorRow
        {
            private readonly Border _swatch;
            private readonly TextBlock _valueText;
            private readonly ColorPicker _picker;
            private readonly Color _automaticPreview;
            private bool _updatingPicker;

            public string Hex { get; private set; }

            public FrameworkElement Root { get; }

            public ColorRow(string label, string hex, Color automaticPreview)
            {
                Hex = ColorUtil.IsValidHex(hex) ? hex : string.Empty;
                _automaticPreview = automaticPreview;

                _swatch = new Border
                {
                    Width = 24,
                    Height = 24,
                    CornerRadius = new CornerRadius(4),
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(Colors.Gray)
                };

                _valueText = new TextBlock { VerticalAlignment = VerticalAlignment.Center };

                var buttonContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
                buttonContent.Children.Add(_swatch);
                buttonContent.Children.Add(_valueText);

                Color startColor = ColorUtil.TryParse(Hex, out Color parsed) ? parsed : automaticPreview;

                _picker = new ColorPicker
                {
                    IsAlphaEnabled = false,
                    IsMoreButtonVisible = true,
                    Color = startColor
                };

                var useDefault = new HyperlinkButton
                {
                    Content = "Use default",
                    Padding = new Thickness(0),
                    Margin = new Thickness(0, 8, 0, 0)
                };

                var flyoutContent = new StackPanel();
                flyoutContent.Children.Add(_picker);
                flyoutContent.Children.Add(useDefault);

                var flyout = new Flyout { Content = flyoutContent };

                var button = new Button { Content = buttonContent, Flyout = flyout, MinWidth = 150 };

                _picker.ColorChanged += (_, e) =>
                {
                    if (_updatingPicker)
                    {
                        return;
                    }

                    Hex = ColorUtil.ToHex(e.NewColor);
                    Refresh();
                };

                useDefault.Click += (_, _) =>
                {
                    SetAutomatic();
                    flyout.Hide();
                };

                var row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition());
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var labelText = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(button, 1);
                row.Children.Add(labelText);
                row.Children.Add(button);
                Root = row;

                Refresh();
            }

            public void SetAutomatic()
            {
                Hex = string.Empty;

                _updatingPicker = true;
                _picker.Color = _automaticPreview;
                _updatingPicker = false;

                Refresh();
            }

            private void Refresh()
            {
                if (ColorUtil.TryParse(Hex, out Color chosen))
                {
                    _swatch.Background = new SolidColorBrush(chosen);
                    _valueText.Text = Hex;
                }
                else
                {
                    _swatch.Background = new SolidColorBrush(_automaticPreview);
                    _valueText.Text = "Default";
                }
            }
        }
    }
}
