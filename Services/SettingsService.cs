using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using DatabaseDock.Models;
using Microsoft.Win32;

namespace DatabaseDock.Services
{
    public class SettingsService
    {
        private const string AppName = "DatabaseDock";
        private const string SettingsFileName = "settings.json";
        private readonly string _settingsFilePath;

        public SettingsService()
        {
            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                AppName);

            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }

            _settingsFilePath = Path.Combine(appDataPath, SettingsFileName);
        }

        public async Task<List<DatabaseContainer>> LoadDatabasesAsync()
        {
            if (!File.Exists(_settingsFilePath))
            {
                // Create default settings with predefined databases
                var defaultDatabases = new List<DatabaseContainer>
                {
                    DatabaseContainer.CreateMySql(),
                    DatabaseContainer.CreateMsSql(),
                    DatabaseContainer.CreatePostgreSql(),
                    DatabaseContainer.CreateRedis()
                };

                await SaveDatabasesAsync(defaultDatabases);
                return defaultDatabases;
            }

            try
            {
                string json = await File.ReadAllTextAsync(_settingsFilePath);
                var options = new JsonSerializerOptions
                {
                    ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve
                };
                var settings = JsonSerializer.Deserialize<AppSettings>(json, options);
                return settings?.Databases ?? new List<DatabaseContainer>();
            }
            catch (Exception)
            {
                // If there's an error loading settings, return default databases
                return new List<DatabaseContainer>
                {
                    DatabaseContainer.CreateMySql(),
                    DatabaseContainer.CreateMsSql(),
                    DatabaseContainer.CreatePostgreSql(),
                    DatabaseContainer.CreateRedis()
                };
            }
        }

        public async Task SaveDatabasesAsync(List<DatabaseContainer> databases)
        {
            // Ensure Type property is set for all databases
            foreach (var database in databases)
            {
                // If Type is null or empty, set it based on database name
                if (string.IsNullOrEmpty(database.Type))
                {
                    switch (database.Name.ToLowerInvariant())
                    {
                        case "mysql":
                            database.Type = "mysql";
                            break;
                        case "mssql":
                            database.Type = "mssql";
                            break;
                        case "postgresql":
                            database.Type = "postgresql";
                            break;
                        case "redis":
                            database.Type = "redis";
                            break;
                    }
                }
            }
            
            var settings = new AppSettings
            {
                Databases = databases,
                StartWithWindows = IsStartWithWindowsEnabled(),
                MinimizeToTray = true
            };

            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true,
                ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve
            });

            await File.WriteAllTextAsync(_settingsFilePath, json);
        }

        public bool IsStartWithWindowsEnabled()
        {
            using var key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", false);
            return key?.GetValue(AppName) != null;
        }

        public void SetStartWithWindows(bool enable)
        {
            using var key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            
            if (enable)
            {
                string appPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                key?.SetValue(AppName, appPath);
            }
            else
            {
                key?.DeleteValue(AppName, false);
            }
        }
    }

    public class AppSettings
    {
        public List<DatabaseContainer> Databases { get; set; } = new List<DatabaseContainer>();
        public bool StartWithWindows { get; set; }
        public bool MinimizeToTray { get; set; }
    }
}
