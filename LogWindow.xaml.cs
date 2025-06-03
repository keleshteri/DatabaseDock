using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DatabaseDock.Models;
using DatabaseDock.Services;

namespace DatabaseDock
{
    public partial class LogWindow : Window, INotifyPropertyChanged
    {
        private readonly LoggingService _loggingService;
        private ObservableCollection<LogEntry> _currentLogs;
        private string _selectedDatabaseName;

        public LogWindow(LoggingService loggingService)
        {
            _loggingService = loggingService;
            _currentLogs = _loggingService.GetApplicationLogs();

            InitializeComponent();
            DataContext = this;

            // Add database tabs dynamically
            UpdateDatabaseTabs();
            
            // Default to application logs tab
            LogTabs.SelectedItem = AppLogsTab;
        }

        public ObservableCollection<LogEntry> CurrentLogs
        {
            get => _currentLogs;
            set
            {
                _currentLogs = value;
                OnPropertyChanged();
            }
        }

        public void UpdateDatabaseTabs()
        {
            // First, preserve the selected database name if there is one
            var selectedTab = LogTabs.SelectedItem as TabItem;
            string previousSelectedDb = null;
            
            if (selectedTab != null && selectedTab != AppLogsTab && selectedTab != AllDockerLogsTab)
            {
                previousSelectedDb = selectedTab.Header.ToString();
            }

            // Remove all database-specific tabs
            for (int i = LogTabs.Items.Count - 1; i >= 0; i--)
            {
                var tabItem = LogTabs.Items[i] as TabItem;
                if (tabItem != AppLogsTab && tabItem != AllDockerLogsTab)
                {
                    LogTabs.Items.RemoveAt(i);
                }
            }

            // Add tabs for each database
            foreach (var dbName in _loggingService.GetDatabaseNames())
            {
                var logListView = new ListView
                {
                    ItemsSource = _loggingService.GetDatabaseLogs(dbName),
                    ItemTemplate = LogItemsListView.ItemTemplate,
                    Background = Brushes.White,
                    FontFamily = new FontFamily("Consolas")
                };
                
                var tabItem = new TabItem
                {
                    Header = dbName,
                    Content = logListView
                };
                
                LogTabs.Items.Add(tabItem);

                // If this was the previously selected tab, select it again
                if (dbName == previousSelectedDb)
                {
                    LogTabs.SelectedItem = tabItem;
                }
            }

            // If no tab is selected, select the application logs tab
            if (LogTabs.SelectedItem == null)
            {
                LogTabs.SelectedItem = AppLogsTab;
            }
        }

        private void LogTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedTab = LogTabs.SelectedItem as TabItem;
            if (selectedTab == null) return;

            if (selectedTab == AppLogsTab)
            {
                // Show application logs only
                CurrentLogs = _loggingService.GetApplicationLogs();
                _selectedDatabaseName = null;
            }
            else if (selectedTab == AllDockerLogsTab)
            {
                // Show all docker logs
                CurrentLogs = _loggingService.GetAllDockerLogs();
                _selectedDatabaseName = null;
            }
            else
            {
                // Show logs for specific database
                _selectedDatabaseName = selectedTab.Header.ToString();
                CurrentLogs = _loggingService.GetDatabaseLogs(_selectedDatabaseName);
            }

            StatusTextBlock.Text = $"Showing {CurrentLogs.Count} log entries";
        }

        private void ClearLogs_Click(object sender, RoutedEventArgs e)
        {
            CurrentLogs.Clear();
            StatusTextBlock.Text = "Logs cleared";
        }

        private void CopyLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var logText = string.Join(Environment.NewLine, CurrentLogs.Select(l => l.ToString()));
                Clipboard.SetText(logText);
                StatusTextBlock.Text = "Logs copied to clipboard";
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Error copying logs: {ex.Message}";
            }
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
