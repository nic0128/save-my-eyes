using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using save_my_eyes.Services;

namespace save_my_eyes.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        private readonly TimerService _timer;
        private readonly SettingsService _settingsService;
        private readonly AppSettings _settings;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(WindowTitle))]
        private string displayTime = "20:00";

        [ObservableProperty]
        private string sessionHint = string.Empty;

        [ObservableProperty]
        private double progress = 100;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(BreaksText))]
        private int breaksToday;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsBreaking))]
        [NotifyPropertyChangedFor(nameof(ToggleLabel))]
        [NotifyPropertyChangedFor(nameof(ToggleGlyph))]
        [NotifyPropertyChangedFor(nameof(WindowTitle))]
        [NotifyCanExecuteChangedFor(nameof(ResetCommand))]
        [NotifyCanExecuteChangedFor(nameof(TakeBreakNowCommand))]
        private TimerState state = TimerState.Idle;

        public bool IsBreaking => State == TimerState.Breaking;

        public Visibility BreaksVisibility =>
            _settings.ShowBreakCount ? Visibility.Visible : Visibility.Collapsed;

        public string ToggleGlyph => (State == TimerState.Working || State == TimerState.Breaking)
            ? "\uE103"
            : "\uE102";

        public string ToggleLabel => State switch
        {
            TimerState.Idle => "Play",
            TimerState.Paused => "Resume",
            _ => "Pause"
        };

        public string BreaksText => BreaksToday == 1
            ? "1 break taken today"
            : $"{BreaksToday} breaks taken today";

        public string WindowTitle => State switch
        {
            TimerState.Idle => "Save my Eyes",
            TimerState.Breaking => $"Break! {DisplayTime} - Save my Eyes",
            TimerState.Paused => $"Paused {DisplayTime} - Save my Eyes",
            _ => $"{DisplayTime} - Save my Eyes"
        };

        public AppSettings Settings => _settings;

        public event EventHandler? BreakStarted;

        public event EventHandler? BreakCompleted;

        public event EventHandler? SettingsChanged;

        public MainWindowViewModel(DispatcherQueue dispatcherQueue, SettingsService settingsService)
        {
            _settingsService = settingsService;
            _settings = settingsService.Load();

            if (_settings.StatsDate == Today())
            {
                BreaksToday = _settings.BreaksToday;
            }

            _timer = new TimerService(dispatcherQueue);
            _timer.TimeUpdated += OnTimeUpdated;
            _timer.StateChanged += OnTimerStateChanged;
            _timer.BreakStarted += OnTimerBreakStarted;
            _timer.BreakCompleted += OnTimerBreakCompleted;

            _timer.ApplyIntervals(_settings.WorkSeconds, _settings.BreakSeconds);
            RefreshHint();
        }

        [RelayCommand]
        public void Toggle()
        {
            switch (State)
            {
                case TimerState.Idle:
                    _timer.StartWork();
                    break;

                case TimerState.Paused:
                    _timer.Resume();
                    break;

                default:
                    _timer.Pause();
                    break;
            }
        }

        [RelayCommand(CanExecute = nameof(CanReset))]
        private void Reset()
        {
            _timer.Stop();
        }

        private bool CanReset() => State != TimerState.Idle;

        [RelayCommand(CanExecute = nameof(CanTakeBreakNow))]
        private void TakeBreakNow()
        {
            _timer.StartBreak();
        }

        private bool CanTakeBreakNow() => State == TimerState.Working;

        public void ApplySettings(AppSettings updated)
        {
            _settings.CopyPreferencesFrom(updated);
            SettingsService.Clamp(_settings);

            _timer.ApplyIntervals(_settings.WorkSeconds, _settings.BreakSeconds);
            RefreshHint();

            _settingsService.Save(_settings);
            OnPropertyChanged(nameof(BreaksVisibility));
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ResetBreaks()
        {
            BreaksToday = 0;
            SaveStats();
        }

        public void Shutdown()
        {
            _timer.Dispose();
            SaveStats();
        }

        private void OnTimeUpdated(object? sender, int remainingSeconds)
        {
            DisplayTime = FormatTime(remainingSeconds);

            Progress = _timer.PhaseSeconds > 0
                ? remainingSeconds * 100.0 / _timer.PhaseSeconds
                : 0;
        }

        private void OnTimerStateChanged(object? sender, TimerState newState)
        {
            State = newState;
            RefreshHint();
        }

        private void OnTimerBreakStarted(object? sender, EventArgs e)
        {
            BreakStarted?.Invoke(this, EventArgs.Empty);
        }

        private void OnTimerBreakCompleted(object? sender, EventArgs e)
        {
            if (_settings.StatsDate != Today())
            {
                BreaksToday = 0;
            }

            BreaksToday++;
            SaveStats();

            BreakCompleted?.Invoke(this, EventArgs.Empty);
        }

        private void RefreshHint()
        {
            SessionHint = State == TimerState.Breaking
                ? "Focus on something about 20 feet (6 m) away until the timer ends."
                : $"Every {Describe(_settings.WorkSeconds)}, look at something about " +
                  $"20 feet (6 m) away for {Describe(_settings.BreakSeconds)}.";
        }

        private void SaveStats()
        {
            _settings.StatsDate = Today();
            _settings.BreaksToday = BreaksToday;
            _settingsService.Save(_settings);
        }

        private static string Today() => DateTime.Now.ToString("yyyy-MM-dd");

        private static string Describe(int totalSeconds)
        {
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;

            string minutesText = minutes == 1 ? "1 minute" : $"{minutes} minutes";
            string secondsText = seconds == 1 ? "1 second" : $"{seconds} seconds";

            if (minutes == 0) return secondsText;
            if (seconds == 0) return minutesText;
            return $"{minutesText} {secondsText}";
        }

        private static string FormatTime(int totalSeconds)
        {
            totalSeconds = Math.Max(0, totalSeconds);
            return $"{totalSeconds / 60:D2}:{totalSeconds % 60:D2}";
        }
    }
}
