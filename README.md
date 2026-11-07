# Save my Eyes

A small Windows app that reminds you to follow the 20-20-20 rule: every 20 minutes, look at something about 20 feet (6 m) away for 20 seconds.

It sits in the background, counts down, and when it is time for a break it plays a sound and brings its window to the front. When the break is over it goes back to counting.

## Features

- Work and break lengths are both configurable in minutes and seconds (defaults: 20 minutes and 20 seconds).
- A single play/pause button, a reset button, and a "Take a break now" link.
- A sound when a break starts and when it ends. Use the default Windows sounds or pick your own file (wav, mp3, wma, m4a, aac, flac).
- The window can come to the front when a break starts and stays on top for the length of the break. If it was minimized before the break, it minimizes again when the break finishes.
- Minimizing hides the app in the system tray instead of the taskbar. Click the tray icon to bring it back, or right-click it for Open, Play/Pause and Exit. There is a setting to keep it in the taskbar instead.
- The remaining time is shown in the window title and in the tray icon's tooltip.
- Light, dark or Windows-default theme, plus custom accent, background, panel, text and break colors.
- A count of breaks taken today, which can be hidden or reset.

## Settings

Settings and today's break count are stored in `%LocalAppData%\SaveMyEyes\settings.json`. Delete the file to go back to the defaults.

## Project layout

| Path | Purpose |
| --- | --- |
| `MainWindow.xaml`, `MainWindow.xaml.cs` | The main window and its theming |
| `ViewModels/MainWindowViewModel.cs` | UI state and commands |
| `Views/SettingsDialog.cs` | The settings dialog |
| `Services/TimerService.cs` | Work/break timer (works from a clock deadline, so it stays accurate across sleep) |
| `Services/AlertService.cs` | Break sounds, bring-to-front, sound previews |
| `Services/TrayService.cs` | Notification-area icon and minimize-to-tray |
| `Services/WindowMinSize.cs` | Minimum window size |
| `Services/SettingsService.cs` | Loading and saving settings |
| `Services/ColorUtil.cs` | Color helpers |

Built with WinUI 3 (Windows App SDK) and [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet).

## Limitations

- Closing the window with the X button quits the app, and the reminders stop. Minimize it to keep it running.
- There is no single-instance check, so running two copies gives you two sets of reminders.

## License

[MIT](LICENSE)
