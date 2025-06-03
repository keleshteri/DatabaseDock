using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace DatabaseDock.Models
{
    public class DatabaseContainer : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _version = string.Empty;
        private string _status = string.Empty;
        private string _containerId = string.Empty;
        private string _iconPath = string.Empty;
        private string _volumePath = string.Empty;
        private int _port;
        private string _connectionString = string.Empty;
        private string _actionButtonText = string.Empty;
        private SolidColorBrush? _statusColor;
        private string _statusColorHex = "#808080"; // Default gray
        private string _password = string.Empty;
        private string _type = string.Empty;
        private bool _connectionTested = false;
        private bool _connectionSuccess = false;
        private string _connectionMessage = string.Empty;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Version
        {
            get => _version;
            set => SetProperty(ref _version, value);
        }

        public string Status
        {
            get => _status;
            set
            {
                if (SetProperty(ref _status, value))
                {
                    // Update status color based on status
                    _statusColorHex = value.ToLower() switch
                    {
                        "running" => "#008000", // Green
                        "stopped" => "#FF0000", // Red
                        "starting" => "#FFA500", // Orange
                        "stopping" => "#FFA500", // Orange
                        _ => "#808080" // Gray
                    };

                    // Clear the cached brush so it will be recreated
                    _statusColor = null;

                    // Update action button text based on status
                    ActionButtonText = value.ToLower() == "running" ? "Stop" : "Start";
                }
            }
        }

        public string ContainerId
        {
            get => _containerId;
            set => SetProperty(ref _containerId, value);
        }

        public string IconPath
        {
            get => _iconPath;
            set => SetProperty(ref _iconPath, value);
        }

        public string VolumePath
        {
            get => _volumePath;
            set => SetProperty(ref _volumePath, value);
        }

        public int Port
        {
            get => _port;
            set => SetProperty(ref _port, value);
        }

        public string ConnectionString
        {
            get => _connectionString;
            set => SetProperty(ref _connectionString, value);
        }

        public string ActionButtonText
        {
            get => _actionButtonText;
            set => SetProperty(ref _actionButtonText, value);
        }

        [JsonIgnore]
        public SolidColorBrush StatusColor
        {
            get 
            {
                if (_statusColor == null)
                {
                    // Convert hex string to SolidColorBrush
                    try
                    {
                        var color = (Color)ColorConverter.ConvertFromString(_statusColorHex);
                        _statusColor = new SolidColorBrush(color);
                    }
                    catch
                    {
                        _statusColor = new SolidColorBrush(Colors.Gray);
                    }
                }
                return _statusColor;
            }
            set 
            {
                if (SetProperty(ref _statusColor, value) && value != null)
                {
                    // Store the color as a hex string for serialization
                    _statusColorHex = value.Color.ToString();
                }
            }
        }

        // This property is used for serialization
        public string StatusColorHex
        {
            get => _statusColorHex;
            set => SetProperty(ref _statusColorHex, value);
        }

        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        public string Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
        }

        public bool ConnectionTested
        {
            get => _connectionTested;
            set => SetProperty(ref _connectionTested, value);
        }

        public bool ConnectionSuccess
        {
            get => _connectionSuccess;
            set => SetProperty(ref _connectionSuccess, value);
        }

        public string ConnectionMessage
        {
            get => _connectionMessage;
            set => SetProperty(ref _connectionMessage, value);
        }

        // Default values for different database types
        public static DatabaseContainer CreateMySql()
        {
            return new DatabaseContainer
            {
                Name = "MySQL",
                Type = "mysql",
                Version = "8.0",
                Status = "Stopped",
                IconPath = "/Resources/Icons/mysql_icon.png",
                Port = 3306,
                VolumePath = "D:\\DockerVolumes\\mysql",
                Password = "12345678",
                ConnectionString = "Server=localhost;Port=3306;Database=mysql;User=root;Password=12345678;"
            };
        }

        public static DatabaseContainer CreateMsSql()
        {
            return new DatabaseContainer
            {
                Name = "MSSQL",
                Type = "mssql",
                Version = "2022",
                Status = "Stopped",
                IconPath = "/Resources/Icons/mssql_icon.png",
                Port = 1433,
                VolumePath = "D:\\DockerVolumes\\mssql",
                Password = "P@ssw0rd",
                ConnectionString = "Server=localhost,1433;Database=master;User Id=sa;Password=P@ssw0rd;TrustServerCertificate=True;"
            };
        }

        public static DatabaseContainer CreatePostgreSql()
        {
            return new DatabaseContainer
            {
                Name = "PostgreSQL",
                Type = "postgresql",
                Version = "15",
                Status = "Stopped",
                IconPath = "/Resources/Icons/postgres_icon.png",
                Port = 5432,
                VolumePath = "D:\\DockerVolumes\\postgres",
                Password = "postgres",
                ConnectionString = "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres;"
            };
        }

        public static DatabaseContainer CreateRedis()
        {
            return new DatabaseContainer
            {
                Name = "Redis",
                Type = "redis",
                Version = "7.0",
                Status = "Stopped",
                IconPath = "/Resources/Icons/redis_icon.png",
                Port = 6379,
                VolumePath = "D:\\DockerVolumes\\redis",
                Password = "",
                ConnectionString = "localhost:6379"
            };
        }

        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
