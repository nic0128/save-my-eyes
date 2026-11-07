using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace save_my_eyes.Services
{
    public sealed class AlertService : IDisposable
    {
        private const uint BeepExclamation = 0x00000030;
        private const uint BeepAsterisk = 0x00000040;

        private static readonly TimeSpan DefaultPreviewLength = TimeSpan.FromMilliseconds(1200);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool MessageBeep(uint uType);

        private readonly Window _window;
        private MediaPlayer? _player;
        private DispatcherQueueTimer? _previewTimer;
        private bool _forcedTopmost;
        private bool _minimizeAfterBreak;

        private bool _previewing;
        private bool _previewUsesFile;
        private int _previewGeneration;

        public event EventHandler? PreviewEnded;

        public bool IsPreviewing => _previewing;

        public AlertService(Window window)
        {
            _window = window;
        }

        public void NotifyBreakStarted(AppSettings settings)
        {
            if (settings.PlaySounds)
            {
                PlayAlertSound(settings.StartSoundPath, BeepExclamation);
            }

            if (settings.BringToFront)
            {
                BringToFront();
            }
        }

        public void NotifyBreakCompleted(AppSettings settings)
        {
            if (settings.PlaySounds)
            {
                PlayAlertSound(settings.EndSoundPath, BeepAsterisk);
            }
        }

        public bool ConsumeMinimizeAfterBreak()
        {
            bool value = _minimizeAfterBreak;
            _minimizeAfterBreak = false;
            return value;
        }

        public void ReleaseTopmost()
        {
            _minimizeAfterBreak = false;

            if (!_forcedTopmost)
            {
                return;
            }

            _forcedTopmost = false;

            try
            {
                if (_window.AppWindow.Presenter is OverlappedPresenter presenter)
                {
                    presenter.IsAlwaysOnTop = false;
                }
            }
            catch (Exception)
            {
            }
        }

        public void StartPreview(string? customPath, bool isStartSound)
        {
            StopPreview();

            _previewing = true;
            _previewGeneration++;
            _previewUsesFile = TryPlayFile(customPath);

            if (!_previewUsesFile)
            {
                MessageBeep(isStartSound ? BeepExclamation : BeepAsterisk);

                if (_previewTimer == null)
                {
                    _previewTimer = _window.DispatcherQueue.CreateTimer();
                    _previewTimer.IsRepeating = false;
                    _previewTimer.Interval = DefaultPreviewLength;
                    _previewTimer.Tick += OnPreviewTimerTick;
                }

                _previewTimer.Start();
            }
        }

        public void StopPreview()
        {
            if (!_previewing)
            {
                return;
            }

            try
            {
                if (_previewUsesFile)
                {
                    _player?.Pause();
                }
            }
            catch (Exception)
            {
            }

            FinishPreview();
        }

        public void Dispose()
        {
            _previewTimer?.Stop();

            if (_player != null)
            {
                _player.MediaEnded -= OnMediaEnded;
                _player.MediaFailed -= OnMediaFailed;
                _player.Dispose();
                _player = null;
            }
        }

        private void PlayAlertSound(string? customPath, uint fallbackBeep)
        {
            StopPreview();

            if (!TryPlayFile(customPath))
            {
                MessageBeep(fallbackBeep);
            }
        }

        private bool TryPlayFile(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return false;
            }

            try
            {
                if (_player == null)
                {
                    _player = new MediaPlayer();
                    _player.MediaEnded += OnMediaEnded;
                    _player.MediaFailed += OnMediaFailed;
                }

                var previous = _player.Source as MediaSource;
                _player.Source = MediaSource.CreateFromUri(new Uri(path));
                previous?.Dispose();

                _player.Play();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void FinishPreview()
        {
            if (!_previewing)
            {
                return;
            }

            _previewing = false;
            _previewTimer?.Stop();
            PreviewEnded?.Invoke(this, EventArgs.Empty);
        }

        private void OnPreviewTimerTick(DispatcherQueueTimer sender, object args)
        {
            sender.Stop();
            FinishPreview();
        }

        private void OnMediaEnded(MediaPlayer sender, object args)
        {
            int generation = Volatile.Read(ref _previewGeneration);

            _window.DispatcherQueue.TryEnqueue(() =>
            {
                if (_previewing && _previewUsesFile && generation == _previewGeneration)
                {
                    FinishPreview();
                }
            });
        }

        private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            _window.DispatcherQueue.TryEnqueue(() =>
            {
                if (_previewing && _previewUsesFile)
                {
                    MessageBeep(BeepExclamation);
                    FinishPreview();
                }
            });
        }

        private void BringToFront()
        {
            try
            {
                bool wasHidden = !_window.AppWindow.IsVisible;
                bool wasMinimized = false;

                if (_window.AppWindow.Presenter is OverlappedPresenter presenter)
                {
                    wasMinimized = presenter.State == OverlappedPresenterState.Minimized;

                    if (wasMinimized)
                    {
                        presenter.Restore();
                    }

                    presenter.IsAlwaysOnTop = true;
                    _forcedTopmost = true;
                }

                if (wasHidden || wasMinimized)
                {
                    _minimizeAfterBreak = true;
                }

                _window.AppWindow.Show();
                _window.Activate();
            }
            catch (Exception)
            {
            }
        }
    }
}
