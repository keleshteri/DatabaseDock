using System;
using System.Windows;
using System.Windows.Forms;
using DatabaseDock.Models;
using DatabaseDock.Services;

namespace DatabaseDock
{
    public partial class DatabaseSettingsWindow : Window
    {
        private readonly DatabaseContainer _database;
        private readonly SettingsService _settingsService;

        public DatabaseSettingsWindow(DatabaseContainer database)
        {
            InitializeComponent();
            
            _database = database;
            _settingsService = new SettingsService();
            
            // Load database settings into UI
            LoadDatabaseSettings();
        }

        private void LoadDatabaseSettings()
        {
            // Set database info
            DatabaseTypeTextBox.Text = _database.Name;
            VersionTextBox.Text = _database.Version;
            
            // Set connection settings
            PortTextBox.Text = _database.Port.ToString();
            
            // Ensure database Type is set correctly based on Name if it's empty
            if (string.IsNullOrEmpty(_database.Type))
            {
                _database.Type = _database.Name.ToLowerInvariant();
            }
            
            // Extract username and password from connection string if available
            string dbType = _database.Type.ToLowerInvariant();
            
            if (dbType == "mysql")
            {
                UsernameTextBox.Text = "root";
                PasswordBox.Password = _database.Password ?? "password";
            }
            else if (dbType == "mssql")
            {
                UsernameTextBox.Text = "sa";
                PasswordBox.Password = _database.Password ?? "P@ssw0rd";
            }
            else if (dbType == "postgresql")
            {
                UsernameTextBox.Text = "postgres";
                PasswordBox.Password = _database.Password ?? "postgres";
            }
            else if (dbType == "redis")
            {
                UsernameTextBox.Text = "";
                PasswordBox.Password = _database.Password ?? "";
            }
            
            // Set volume path
            VolumePathTextBox.Text = _database.VolumePath;
            
            // Set advanced settings
            StartWithWindowsCheckBox.IsChecked = _settingsService.IsStartWithWindowsEnabled();
        }

        private void BrowseVolumePath_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Select volume path for database data",
                UseDescriptionForTitle = true,
                SelectedPath = VolumePathTextBox.Text
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                VolumePathTextBox.Text = dialog.SelectedPath;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Update database settings
                _database.Version = VersionTextBox.Text;
                _database.Port = int.Parse(PortTextBox.Text);
                _database.VolumePath = VolumePathTextBox.Text;
                
                // Update connection string based on database type
                UpdateConnectionString();
                
                // Set start with Windows
                _settingsService.SetStartWithWindows(StartWithWindowsCheckBox.IsChecked ?? false);
                
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error saving settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateConnectionString()
        {
            string username = UsernameTextBox.Text;
            string password = PasswordBox.Password;
            
            // Always store the password in the database object
            _database.Password = password;
            
            // Make sure Type is set correctly if it's empty
            if (string.IsNullOrEmpty(_database.Type))
            {
                _database.Type = _database.Name.ToLowerInvariant();
            }
            
            // Use Type property for determining connection string format
            string dbType = _database.Type.ToLowerInvariant();
            
            switch (dbType)
            {
                case "mysql":
                    _database.ConnectionString = $"Server=localhost;Port={_database.Port};Database=mysql;User={username};Password={password};";
                    break;
                case "mssql":
                    _database.ConnectionString = $"Server=localhost,{_database.Port};Database=master;User Id={username};Password={password};TrustServerCertificate=True;";
                    break;
                case "postgresql":
                    _database.ConnectionString = $"Host=localhost;Port={_database.Port};Database=postgres;Username={username};Password={password};";
                    break;
                case "redis":
                    _database.ConnectionString = $"localhost:{_database.Port}";
                    break;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
