using System;

namespace DatabaseDock.Models
{
    public enum LogType
    {
        Info,
        Warning,
        Error,
        DockerLog,
        ConfigLog
    }

    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Message { get; set; }
        public LogType Type { get; set; }
        public string DatabaseName { get; set; }

        public LogEntry(string message, LogType type = LogType.Info, string databaseName = null)
        {
            Timestamp = DateTime.Now;
            Message = message;
            Type = type;
            DatabaseName = databaseName;
        }

        public override string ToString()
        {
            return $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Type}] {(string.IsNullOrEmpty(DatabaseName) ? "" : $"[{DatabaseName}] ")}{Message}";
        }
    }
}
