using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DatabaseDock.Models;
using DatabaseDock.Services;

namespace DatabaseDock
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly DockerService _dockerService;
        private readonly SettingsService _settingsService;
        private ObservableCollection<DatabaseContainer> _databases;
        private bool _isClosing;
        private bool _isMinimizingToTray;

        public MainWindow()
        {
            InitializeComponent();

            // Initialize services
            _dockerService = new DockerService();
            _settingsService = new SettingsService();
            _databases = new ObservableCollection<DatabaseContainer>();

            // Set data context
            DataContext = this;
            DatabaseListView.ItemsSource = _databases;

            // Load databases and check Docker status
            Loaded += MainWindow_Loaded;
        }

        public ObservableCollection<DatabaseContainer> Databases => _databases;

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDatabases();
            await CheckDockerStatus();
        }

        private async Task LoadDatabases()
        {
            try
            {
                var databases = await _settingsService.LoadDatabasesAsync();
                _databases.Clear();

                foreach (var database in databases)
                {
                    _databases.Add(database);
                }

                StatusTextBlock.Text = "Databases loaded successfully";
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Error loading databases: {ex.Message}";
                MessageBox.Show($"Error loading databases: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task CheckDockerStatus()
        {
            try
            {
                bool isDockerRunning = await _dockerService.IsDockerRunning();

                if (!isDockerRunning)
                {
                    StatusTextBlock.Text = "Docker is not running. Please start Docker Desktop.";
                    MessageBox.Show("Docker is not running. Please start Docker Desktop and restart the application.", 
                        "Docker Not Running", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Get running containers and update database statuses
                await RefreshContainerStatuses();
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Error connecting to Docker: {ex.Message}";
                MessageBox.Show($"Error connecting to Docker: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task RefreshContainerStatuses()
        {
            try
            {
                StatusTextBlock.Text = "Refreshing container statuses...";

                var runningContainers = await _dockerService.GetRunningContainers();

                foreach (var database in _databases)
                {
                    // Check if this database has a container ID
                    if (!string.IsNullOrEmpty(database.ContainerId))
                    {
                        // Get current status from Docker
                        string status = await _dockerService.GetContainerStatus(database.ContainerId);

                        // Update status based on Docker response
                        if (status.ToLower() == "running")
                        {
                            database.Status = "Running";
                        }
                        else
                        {
                            database.Status = "Stopped";
                        }
                    }
                    else
                    {
                        database.Status = "Stopped";
                    }
                }

                StatusTextBlock.Text = "Container statuses refreshed";
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Error refreshing container statuses: {ex.Message}";
            }
        }

        private async void ToggleDatabase_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.CommandParameter is DatabaseContainer database)
            {
                try
                {
                    if (database.Status.ToLower() == "running")
                    {
                        // Stop the database
                        database.Status = "Stopping";
                        StatusTextBlock.Text = $"Stopping {database.Name}...";

                        await _dockerService.StopDatabaseContainer(database.ContainerId);
                        database.Status = "Stopped";
                        StatusTextBlock.Text = $"{database.Name} stopped successfully";
                    }
                    else if (database.Status.ToLower() == "stopped")
                    {
                        // Start the database
                        database.Status = "Starting";
                        StatusTextBlock.Text = $"Starting {database.Name}...";

                        string containerId = await _dockerService.StartDatabaseContainer(database);
                        database.ContainerId = containerId;
                        database.Status = "Running";
                        StatusTextBlock.Text = $"{database.Name} started successfully";
                    }

                    // Save updated database information
                    await _settingsService.SaveDatabasesAsync(_databases.ToList());
                }
                catch (Exception ex)
                {
                    database.Status = "Error";
                    StatusTextBlock.Text = $"Error: {ex.Message}";
                    MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void DatabaseSettings_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.CommandParameter is DatabaseContainer database)
            {
                var settingsWindow = new DatabaseSettingsWindow(database)
                {
                    Owner = this
                };

                if (settingsWindow.ShowDialog() == true)
                {
                    // Save updated settings
                    await _settingsService.SaveDatabasesAsync(_databases.ToList());
                    StatusTextBlock.Text = $"{database.Name} settings updated";
                }
            }
        }

        private async void RefreshStatus_Click(object sender, RoutedEventArgs e)
        {
            await RefreshContainerStatuses();
        }

        private void AddDatabase_Click(object sender, RoutedEventArgs e)
        {
            // For now, we'll just show a message
            MessageBox.Show("This feature will be implemented in a future version.", "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            AboutWindow aboutWindow = new AboutWindow();
            aboutWindow.Owner = this; 
            aboutWindow.ShowDialog();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!_isClosing && !_isMinimizingToTray)
            {
                e.Cancel = true;
                _isMinimizingToTray = true;
                Hide();
                _isMinimizingToTray = false;
            }
        }

        private void TrayIcon_TrayLeftMouseDown(object sender, RoutedEventArgs e)
        {
            ShowWindow();
        }

        private void ShowWindow_Click(object sender, RoutedEventArgs e)
        {
            ShowWindow();
        }

        private void ShowWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private async void StartAllDatabases_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusTextBlock.Text = "Starting all databases...";

                foreach (var database in _databases.Where(d => d.Status.ToLower() == "stopped"))
                {
                    database.Status = "Starting";
                    string containerId = await _dockerService.StartDatabaseContainer(database);
                    database.ContainerId = containerId;
                    database.Status = "Running";
                }

                // Save updated database information
                await _settingsService.SaveDatabasesAsync(_databases.ToList());
                StatusTextBlock.Text = "All databases started successfully";
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Error starting databases: {ex.Message}";
                MessageBox.Show($"Error starting databases: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void StopAllDatabases_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusTextBlock.Text = "Stopping all databases...";

                foreach (var database in _databases.Where(d => d.Status.ToLower() == "running" && !string.IsNullOrEmpty(d.ContainerId)))
                {
                    database.Status = "Stopping";
                    await _dockerService.StopDatabaseContainer(database.ContainerId);
                    database.Status = "Stopped";
                }

                // Save updated database information
                await _settingsService.SaveDatabasesAsync(_databases.ToList());
                StatusTextBlock.Text = "All databases stopped successfully";
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Error stopping databases: {ex.Message}";
                MessageBox.Show($"Error stopping databases: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExitApplication_Click(object sender, RoutedEventArgs e)
        {
            _isClosing = true;
            Close();
        }
    }
}