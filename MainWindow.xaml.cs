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
        private readonly DatabaseConnectionService _connectionService;
        private ObservableCollection<DatabaseContainer> _databases;
        private bool _isClosing;
        private bool _isMinimizingToTray;
        private LogWindow _logWindow;

        public MainWindow()
        {
            InitializeComponent();

            // Initialize services
            _dockerService = new DockerService();
            _settingsService = new SettingsService();
            _connectionService = new DatabaseConnectionService(_dockerService.LoggingService);
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
                _dockerService.LoggingService.LogInfo("Refreshing container statuses");

                var runningContainers = await _dockerService.GetRunningContainers();

                foreach (var database in _databases)
                {
                    // Check if this database has a container ID
                    if (!string.IsNullOrEmpty(database.ContainerId))
                    {
                        // Get current status from Docker
                        string status = await _dockerService.GetContainerStatus(database.ContainerId, database.Name);

                        // Update status based on Docker response
                        if (status.ToLower() == "running")
                        {
                            database.Status = "Running";
                            
                            // Ensure we're streaming logs for running containers
                            await _dockerService.LoggingService.StartContainerLogStream(database.ContainerId, database.Name);
                        }
                        else
                        {
                            database.Status = "Stopped";
                            _dockerService.LoggingService.StopContainerLogStream(database.Name);
                        }
                    }
                    else
                    {
                        database.Status = "Stopped";
                    }
                }

                StatusTextBlock.Text = "Container statuses refreshed";
                _dockerService.LoggingService.LogInfo("Container statuses refreshed");
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Error refreshing container statuses: {ex.Message}";
                _dockerService.LoggingService.LogError($"Error refreshing container statuses: {ex.Message}");
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

                        // Create a progress reporter for UI updates
                        var progress = new Progress<string>(message =>
                        {
                            StatusTextBlock.Text = message;
                        });

                        await _dockerService.StopDatabaseContainer(database.ContainerId, database.Name);
                        database.Status = "Stopped";
                        // Reset connection status when stopped
                        database.ConnectionTested = false;
                        database.ConnectionSuccess = false;
                        database.ConnectionMessage = string.Empty;
                        StatusTextBlock.Text = $"{database.Name} stopped successfully";
                    }
                    else if (database.Status.ToLower() == "stopped")
                    {
                        // Start the database
                        database.Status = "Starting";
                        StatusTextBlock.Text = $"Starting {database.Name}...";

                        // Create a progress reporter for UI updates
                        var progress = new Progress<string>(message =>
                        {
                            StatusTextBlock.Text = message;
                        });

                        string containerId = await _dockerService.StartDatabaseContainer(database, progress);
                        database.ContainerId = containerId;
                        database.Status = "Running";
                        database.ConnectionTested = false;
                        StatusTextBlock.Text = $"{database.Name} started successfully";
                        
                        // Auto-test connection after database is started
                        await TestDatabaseConnectionAsync(database);
                    }

                    // Save updated database information
                    await _settingsService.SaveDatabasesAsync(_databases.ToList());
                }
                catch (Exception ex)
                {
                    database.Status = "Error";
                    StatusTextBlock.Text = $"Error: {ex.Message}";
                    _dockerService.LoggingService.LogError($"Error toggling database: {ex.Message}", database.Name);
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
                _dockerService.LoggingService.LogInfo("Starting all databases");

                foreach (var database in _databases.Where(d => d.Status.ToLower() == "stopped"))
                {
                    database.Status = "Starting";
                    
                    // Create a progress reporter for UI updates
                    var progress = new Progress<string>(message =>
                    {
                        StatusTextBlock.Text = $"{database.Name}: {message}";
                    });
                    
                    string containerId = await _dockerService.StartDatabaseContainer(database, progress);
                    database.ContainerId = containerId;
                    database.Status = "Running";
                    database.ConnectionTested = false;
                    
                    // Auto-test connection after database is started
                    await TestDatabaseConnectionAsync(database);
                }

                // Save updated database information
                await _settingsService.SaveDatabasesAsync(_databases.ToList());
                StatusTextBlock.Text = "All databases started successfully";
                _dockerService.LoggingService.LogInfo("All databases started successfully");
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Error starting databases: {ex.Message}";
                _dockerService.LoggingService.LogError($"Error starting databases: {ex.Message}");
                MessageBox.Show($"Error starting databases: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void StopAllDatabases_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusTextBlock.Text = "Stopping all databases...";
                _dockerService.LoggingService.LogInfo("Stopping all databases");

                foreach (var database in _databases.Where(d => d.Status.ToLower() == "running" && !string.IsNullOrEmpty(d.ContainerId)))
                {
                    database.Status = "Stopping";
                    await _dockerService.StopDatabaseContainer(database.ContainerId, database.Name);
                    database.Status = "Stopped";
                    // Reset connection status when stopped
                    database.ConnectionTested = false;
                    database.ConnectionSuccess = false;
                    database.ConnectionMessage = string.Empty;
                }

                // Save updated database information
                await _settingsService.SaveDatabasesAsync(_databases.ToList());
                StatusTextBlock.Text = "All databases stopped successfully";
                _dockerService.LoggingService.LogInfo("All databases stopped successfully");
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Error stopping databases: {ex.Message}";
                _dockerService.LoggingService.LogError($"Error stopping databases: {ex.Message}");
                MessageBox.Show($"Error stopping databases: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExitApplication_Click(object sender, RoutedEventArgs e)
        {
            _isClosing = true;
            
            // Stop all log streams
            _dockerService.LoggingService.StopAllLogStreams();
            _dockerService.LoggingService.LogInfo("Application exiting");
            
            Close();
        }
        
        private void ViewLogs_Click(object sender, RoutedEventArgs e)
        {
            if (_logWindow == null || !_logWindow.IsVisible)
            {
                _logWindow = new LogWindow(_dockerService.LoggingService);
                _logWindow.Owner = this;
                _logWindow.Show();
            }
            else
            {
                _logWindow.Activate();
            }
        }
        
        private async void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.CommandParameter is DatabaseContainer database)
            {
                button.IsEnabled = false;
                await TestDatabaseConnectionAsync(database, button);
                button.IsEnabled = true;
            }
        }
        
        private async Task TestDatabaseConnectionAsync(DatabaseContainer database, Button? button = null)
        {
            if (database == null) return;
            
            try
            {
                // Only test connections for running databases
                if (database.Status.ToLower() != "running")
                {
                    if (button != null) // Only show message if triggered by manual button click
                    {
                        MessageBox.Show($"Database {database.Name} is not running. Please start it first.", 
                            "Database Not Running", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    return;
                }
                
                // Update UI
                StatusTextBlock.Text = $"Testing connection to {database.Name}...";
                _dockerService.LoggingService.LogInfo($"Testing connection to {database.Name}...", database.Name);
                
                // Perform the test
                var result = await _connectionService.TestConnectionAsync(database);
                
                // Update database model with results
                database.ConnectionTested = true;
                database.ConnectionSuccess = result.Success;
                database.ConnectionMessage = result.Success ? 
                    result.Message.Split('.')[0] : // Just show the first part of successful messages
                    result.Message;
                
                // Update UI
                StatusTextBlock.Text = result.Message;
                
                // Save the updated database info
                await _settingsService.SaveDatabasesAsync(_databases.ToList());
            }
            catch (Exception ex)
            {
                database.ConnectionTested = true;
                database.ConnectionSuccess = false;
                database.ConnectionMessage = $"Error: {ex.Message}";
                
                StatusTextBlock.Text = $"Error testing connection: {ex.Message}";
                _dockerService.LoggingService.LogError($"Error testing connection: {ex.Message}", database.Name);
                
                if (button != null) // Only show message if triggered by manual button click
                {
                    MessageBox.Show($"Error testing connection: {ex.Message}", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}