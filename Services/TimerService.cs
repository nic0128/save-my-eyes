using System;
using Microsoft.UI.Dispatching;

namespace save_my_eyes.Services
{
    public enum TimerState
    {
        Idle,
        Working,
        Breaking,
        Paused
    }

    public sealed class TimerService : IDisposable
    {
        private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(250);

        private readonly DispatcherQueueTimer _timer;

        private TimerState _state = TimerState.Idle;
        private TimerState _stateBeforePause = TimerState.Idle;

        private DateTime _deadlineUtc;
        private TimeSpan _pausedRemaining;
        private int _lastReportedSeconds = -1;

        private int _workSeconds = 20 * 60;
        private int _breakSeconds = 20;
        private int _phaseSeconds = 20 * 60;

        public event EventHandler<int>? TimeUpdated;

        public event EventHandler<TimerState>? StateChanged;

        public event EventHandler? BreakStarted;

        public event EventHandler? BreakCompleted;

        public TimerState State => _state;

        public int PhaseSeconds => _phaseSeconds;

        public TimerService(DispatcherQueue dispatcherQueue)
        {
            _timer = dispatcherQueue.CreateTimer();
            _timer.Interval = TickInterval;
            _timer.IsRepeating = true;
            _timer.Tick += OnTick;
        }

        public void ApplyIntervals(int workSeconds, int breakSeconds)
        {
            _workSeconds = Math.Max(1, workSeconds);
            _breakSeconds = Math.Max(1, breakSeconds);

            if (_state == TimerState.Idle)
            {
                _phaseSeconds = _workSeconds;
                Report(_workSeconds);
            }
        }

        public void StartWork()
        {
            BeginPhase(TimerState.Working, _workSeconds);
        }

        public void StartBreak()
        {
            BeginPhase(TimerState.Breaking, _breakSeconds);
            BreakStarted?.Invoke(this, EventArgs.Empty);
        }

        public void Pause()
        {
            if (_state != TimerState.Working && _state != TimerState.Breaking)
            {
                return;
            }

            _pausedRemaining = _deadlineUtc - DateTime.UtcNow;
            if (_pausedRemaining < TimeSpan.Zero)
            {
                _pausedRemaining = TimeSpan.Zero;
            }

            _timer.Stop();

            _stateBeforePause = _state;
            _state = TimerState.Paused;
            StateChanged?.Invoke(this, _state);
        }

        public void Resume()
        {
            if (_state != TimerState.Paused)
            {
                return;
            }

            _deadlineUtc = DateTime.UtcNow + _pausedRemaining;
            _state = _stateBeforePause;
            StateChanged?.Invoke(this, _state);

            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();

            _state = TimerState.Idle;
            _phaseSeconds = _workSeconds;

            StateChanged?.Invoke(this, _state);
            Report(_workSeconds);
        }

        public void Dispose()
        {
            _timer.Stop();
            _timer.Tick -= OnTick;
        }

        private void BeginPhase(TimerState state, int seconds)
        {
            _phaseSeconds = seconds;
            _deadlineUtc = DateTime.UtcNow.AddSeconds(seconds);
            _state = state;

            StateChanged?.Invoke(this, _state);
            Report(seconds);

            _timer.Start();
        }

        private void Report(int seconds)
        {
            _lastReportedSeconds = seconds;
            TimeUpdated?.Invoke(this, seconds);
        }

        private void OnTick(DispatcherQueueTimer sender, object args)
        {
            if (_state != TimerState.Working && _state != TimerState.Breaking)
            {
                _timer.Stop();
                return;
            }

            TimeSpan remaining = _deadlineUtc - DateTime.UtcNow;

            if (remaining <= TimeSpan.Zero)
            {
                OnPhaseElapsed();
                return;
            }

            int seconds = (int)Math.Ceiling(remaining.TotalSeconds);
            if (seconds != _lastReportedSeconds)
            {
                Report(seconds);
            }
        }

        private void OnPhaseElapsed()
        {
            if (_state == TimerState.Working)
            {
                StartBreak();
            }
            else
            {
                BreakCompleted?.Invoke(this, EventArgs.Empty);
                StartWork();
            }
        }
    }
}
