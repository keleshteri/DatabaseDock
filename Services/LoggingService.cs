using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DatabaseDock.Extensions;
using DatabaseDock.Models;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace DatabaseDock.Services
{
    public class LoggingService
    {
        private readonly ObservableCollection<LogEntry> _logs;
        private readonly Dictionary<string, ObservableCollection<LogEntry>> _databaseLogs;
        private readonly DockerClient _dockerClient;
        private readonly int _maxLogEntries = 1000;
        private readonly Dictionary<string, CancellationTokenSource> _logStreamCancellationTokens;

        public LoggingService(DockerClient dockerClient)
        {
            _logs = new ObservableCollection<LogEntry>();
            _databaseLogs = new Dictionary<string, ObservableCollection<LogEntry>>();
            _dockerClient = dockerClient;
            _logStreamCancellationTokens = new Dictionary<string, CancellationTokenSource>();
        }

        public ObservableCollection<LogEntry> Logs => _logs;

        // Getters for the log collections
        public ObservableCollection<LogEntry> GetApplicationLogs() => _logs.Where(l => l.Type != LogType.DockerLog).ToObservableCollection();
        public ObservableCollection<LogEntry> GetAllDockerLogs() => _logs.Where(l => l.Type == LogType.DockerLog).ToObservableCollection();
        public ObservableCollection<LogEntry> GetDatabaseLogs(string databaseName)
        {
            if (!_databaseLogs.ContainsKey(databaseName))
            {
                _databaseLogs[databaseName] = new ObservableCollection<LogEntry>();
            }
            return _databaseLogs[databaseName];
        }
        
        // Get list of all database names that have logs
        public List<string> GetDatabaseNames()
        {
            return _databaseLogs.Keys.ToList();
        }

        public void LogInfo(string message, string databaseName = null)
        {
            AddLogEntry(new LogEntry(message, LogType.Info, databaseName));
        }

        public void LogWarning(string message, string databaseName = null)
        {
            AddLogEntry(new LogEntry(message, LogType.Warning, databaseName));
        }

        public void LogError(string message, string databaseName = null)
        {
            AddLogEntry(new LogEntry(message, LogType.Error, databaseName));
        }

        public void LogDockerMessage(string message, string databaseName)
        {
            AddLogEntry(new LogEntry(message, LogType.DockerLog, databaseName));
        }

        public void LogContainerConfig(ContainerConfigLog configLog, string databaseName)
        {
            AddLogEntry(new LogEntry(configLog.ToString(), LogType.ConfigLog, databaseName));
        }

        private void AddLogEntry(LogEntry entry)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                // Add to global logs
                _logs.Add(entry);
                
                // Trim global logs if needed
                while (_logs.Count > _maxLogEntries)
                {
                    _logs.RemoveAt(0);
                }

                // Add to database-specific logs if applicable
                if (!string.IsNullOrEmpty(entry.DatabaseName))
                {
                    var dbLogs = GetDatabaseLogs(entry.DatabaseName);
                    dbLogs.Add(entry);
                    
                    // Trim database logs if needed
                    while (dbLogs.Count > _maxLogEntries)
                    {
                        dbLogs.RemoveAt(0);
                    }
                }
            });
        }

        public async Task<bool> StartContainerLogStream(string containerId, string databaseName, CancellationToken cancellationToken = default)
        {
            // Cancel any existing stream for this database
            StopContainerLogStream(databaseName);
            
            var tokenSource = new CancellationTokenSource();
            _logStreamCancellationTokens[databaseName] = tokenSource;
            
            var combinedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(tokenSource.Token, cancellationToken);
            
            try
            {
                LogInfo($"Starting log stream for container {containerId}", databaseName);
                
                // Configure log parameters - get logs from the last hour and then follow
                var parameters = new ContainerLogsParameters
                {
                    ShowStdout = true,
                    ShowStderr = true,
                    Follow = true,
                    Timestamps = true,
                    Since = ((DateTimeOffset)DateTime.Now.AddHours(-1)).ToUnixTimeSeconds().ToString()
                };

                // Get the stream
                var stream = await _dockerClient.Containers.GetContainerLogsAsync(containerId, parameters, combinedTokenSource.Token);
                
                // Process the stream
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var buffer = new byte[4096];
                        var multiplexedStream = new DatabaseDock.Models.MultiplexedStream(stream);
                        var currentLine = string.Empty;

                        while (!combinedTokenSource.Token.IsCancellationRequested)
                        {
                            var result = await multiplexedStream.ReadOutputAsync(buffer, 0, buffer.Length, combinedTokenSource.Token);
                            if (result.EOF)
                                break;

                            // Calculate the actual length of data in the buffer
                            // Since we don't have a Count property, we need to determine the length ourselves
                            // A safe approach is to use the buffer length as the data is written to the buffer passed to ReadOutputAsync
                            var data = System.Text.Encoding.UTF8.GetString(buffer);
                            
                            var lines = data.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                            
                            for (int i = 0; i < lines.Length; i++)
                            {
                                if (i == 0 && !string.IsNullOrEmpty(currentLine))
                                {
                                    // Append to existing partial line
                                    currentLine += lines[i];
                                    if (i == lines.Length - 1 && !data.EndsWith("\n") && !data.EndsWith("\r"))
                                    {
                                        // Still not a complete line, wait for more data
                                        continue;
                                    }
                                    LogDockerMessage(currentLine, databaseName);
                                    currentLine = string.Empty;
                                }
                                else if (i == lines.Length - 1 && !data.EndsWith("\n") && !data.EndsWith("\r"))
                                {
                                    // Last part is incomplete, save for next buffer
                                    currentLine = lines[i];
                                }
                                else
                                {
                                    // Complete line, log it
                                    LogDockerMessage(lines[i], databaseName);
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when stream is cancelled
                        LogInfo($"Log stream cancelled for {databaseName}", databaseName);
                    }
                    catch (Exception ex)
                    {
                        LogError($"Error reading container logs: {ex.Message}", databaseName);
                    }
                });
                
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Failed to start log stream: {ex.Message}", databaseName);
                return false;
            }
        }

        public void StopContainerLogStream(string databaseName)
        {
            if (_logStreamCancellationTokens.TryGetValue(databaseName, out var tokenSource))
            {
                tokenSource.Cancel();
                _logStreamCancellationTokens.Remove(databaseName);
                LogInfo($"Stopped log stream for {databaseName}", databaseName);
            }
        }

        public void StopAllLogStreams()
        {
            foreach (var tokenSource in _logStreamCancellationTokens.Values)
            {
                tokenSource.Cancel();
            }
            _logStreamCancellationTokens.Clear();
            LogInfo("Stopped all log streams");
        }
    }
}
