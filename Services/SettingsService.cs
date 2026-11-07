using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace save_my_eyes.Services
{
    public sealed class AppSettings
    {
        public int WorkSeconds { get; set; } = 20 * 60;
        public int BreakSeconds { get; set; } = 20;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? WorkMinutes { get; set; }

        public bool PlaySounds { get; set; } = true;
        public bool BringToFront { get; set; } = true;

        public string StartSoundPath { get; set; } = string.Empty;

        public string EndSoundPath { get; set; } = string.Empty;

        public bool KeepInTaskbar { get; set; }

        public int ThemeMode { get; set; }

        public bool ShowBreakCount { get; set; } = true;

        public string AccentColor { get; set; } = string.Empty;
        public string BackgroundColor { get; set; } = string.Empty;
        public string CardColor { get; set; } = string.Empty;
        public string TextColor { get; set; } = string.Empty;
        public string BreakColor { get; set; } = string.Empty;

        public string StatsDate { get; set; } = string.Empty;
        public int BreaksToday { get; set; }

        public void CopyPreferencesFrom(AppSettings other)
        {
            WorkSeconds = other.WorkSeconds;
            BreakSeconds = other.BreakSeconds;
            PlaySounds = other.PlaySounds;
            BringToFront = other.BringToFront;
            StartSoundPath = other.StartSoundPath;
            EndSoundPath = other.EndSoundPath;
            KeepInTaskbar = other.KeepInTaskbar;
            ThemeMode = other.ThemeMode;
            ShowBreakCount = other.ShowBreakCount;
            AccentColor = other.AccentColor;
            BackgroundColor = other.BackgroundColor;
            CardColor = other.CardColor;
            TextColor = other.TextColor;
            BreakColor = other.BreakColor;
        }
    }

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(AppSettings))]
    internal partial class AppSettingsJsonContext : JsonSerializerContext
    {
    }

    public sealed class SettingsService
    {
        public const int MinWorkSeconds = 5;
        public const int MaxWorkSeconds = 120 * 60;
        public const int MinBreakSeconds = 5;
        public const int MaxBreakSeconds = 60 * 60;

        private readonly string _filePath;

        public SettingsService()
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SaveMyEyes");

            _filePath = Path.Combine(folder, "settings.json");
        }

        public AppSettings Load()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    string json = File.ReadAllText(_filePath);
                    AppSettings? loaded = JsonSerializer.Deserialize(json, AppSettingsJsonContext.Default.AppSettings);

                    if (loaded != null)
                    {
                        Clamp(loaded);
                        return loaded;
                    }
                }
            }
            catch (Exception)
            {
            }

            return new AppSettings();
        }

        public void Save(AppSettings settings)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);

                string json = JsonSerializer.Serialize(settings, AppSettingsJsonContext.Default.AppSettings);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception)
            {
            }
        }

        public static void Clamp(AppSettings settings)
        {
            if (settings.WorkMinutes is int legacyMinutes)
            {
                settings.WorkSeconds = legacyMinutes * 60;
                settings.WorkMinutes = null;
            }

            settings.WorkSeconds = Math.Clamp(settings.WorkSeconds, MinWorkSeconds, MaxWorkSeconds);
            settings.BreakSeconds = Math.Clamp(settings.BreakSeconds, MinBreakSeconds, MaxBreakSeconds);
            settings.BreaksToday = Math.Max(0, settings.BreaksToday);
            settings.ThemeMode = Math.Clamp(settings.ThemeMode, 0, 2);

            settings.StatsDate ??= string.Empty;
            settings.StartSoundPath ??= string.Empty;
            settings.EndSoundPath ??= string.Empty;

            settings.AccentColor = CleanColor(settings.AccentColor);
            settings.BackgroundColor = CleanColor(settings.BackgroundColor);
            settings.CardColor = CleanColor(settings.CardColor);
            settings.TextColor = CleanColor(settings.TextColor);
            settings.BreakColor = CleanColor(settings.BreakColor);
        }

        private static string CleanColor(string? value) =>
            ColorUtil.IsValidHex(value) ? value!.Trim() : string.Empty;
    }
}
