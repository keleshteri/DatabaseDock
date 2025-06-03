using System.IO;
using DatabaseDock.Models;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace DatabaseDock.Services;

public class DockerService
{
    private readonly DockerClient _client;
    private LoggingService _loggingService;

    public DockerService()
    {
        // Connect to Docker daemon
        _client = new DockerClientConfiguration(new Uri("npipe://./pipe/docker_engine"))
            .CreateClient();
        
        // Create logging service
        _loggingService = new LoggingService(_client);
    }

    public LoggingService LoggingService => _loggingService;

    public async Task<bool> IsDockerRunning()
    {
        try
        {
            await _client.System.PingAsync();
            _loggingService.LogInfo("Docker engine is running");
            return true;
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Docker engine is not running: {ex.Message}");
            return false;
        }
    }

    public async Task<List<string>> GetRunningContainers()
    {
        var containers = await _client.Containers.ListContainersAsync(
            new ContainersListParameters
            {
                All = true
            });

        return containers.Select(c => c.ID).ToList();
    }

    public async Task<string> StartDatabaseContainer(DatabaseContainer database, IProgress<string> progress = null)
    {
        try
        {
            string containerName = $"databasedock-{database.Name.ToLower()}-{database.Name.ToLower()}";
            progress?.Report($"Starting {database.Name} database '{database.Name}'...");

            // Get container configuration to extract image name and tag
            var (imageNameWithTag, _, _) = GetContainerConfig(database);
            var parts = imageNameWithTag.Split(':');
            string imageName = parts[0];
            string imageTag = parts.Length > 1 ? parts[1] : "latest";

            // Create a progress handler that logs and forwards to the UI progress reporter
            var combinedProgress = new Progress<string>(message => 
            {
                _loggingService.LogInfo(message, database.Name);
                progress?.Report(message);
            });

            // Ensure the image exists locally, pull if necessary
            _loggingService.LogInfo($"Checking for image {imageName}:{imageTag}", database.Name);
            await EnsureImageExistsAsync(imageName, imageTag, combinedProgress);

            // Ensure volume directory exists
            if (!string.IsNullOrEmpty(database.VolumePath))
            {
                if (!Directory.Exists(database.VolumePath))
                {
                    _loggingService.LogInfo($"Creating volume directory: {database.VolumePath}", database.Name);
                    Directory.CreateDirectory(database.VolumePath);
                }
                else
                {
                    _loggingService.LogInfo($"Using existing volume directory: {database.VolumePath}", database.Name);
                }
            }

            // Check if container already exists
            _loggingService.LogInfo("Checking for existing container", database.Name);
            // Use the already defined containerName variable
            var existingContainers = await _client.Containers.ListContainersAsync(
                new ContainersListParameters
                {
                    All = true,
                    Filters = new Dictionary<string, IDictionary<string, bool>>
                    {
                        {
                            "name", new Dictionary<string, bool>
                            {
                                { containerName, true }
                            }
                        }
                    }
                });

            var existingContainer = existingContainers.FirstOrDefault();

            // If container exists, start it
            if (existingContainer != null)
            {
                _loggingService.LogInfo($"Starting existing container {existingContainer.ID.Substring(0, 12)}", database.Name);
                await _client.Containers.StartContainerAsync(existingContainer.ID, new ContainerStartParameters());
                
                // Create config log for existing container
                var existingConfigLog = new ContainerConfigLog
                {
                    DatabaseName = database.Name,
                    DatabaseType = database.Name.ToLower(),
                    ContainerName = containerName,
                    ContainerId = existingContainer.ID,
                    ImageName = imageName,
                    ImageTag = imageTag,
                    Port = database.Port,
                    VolumePath = database.VolumePath,
                    ConnectionString = database.ConnectionString,
                    VolumeContainerPath = !string.IsNullOrEmpty(database.VolumePath) ? database.Name.ToLower() switch
                    {
                        "mysql" => "/var/lib/mysql",
                        "mssql" => "/var/opt/mssql/data",
                        "postgresql" => "/var/lib/postgresql/data",
                        "redis" => "/data",
                        _ => "unknown"
                    } : null
                };
                
                // Get the container configuration for logging purposes
                var existingContainerConfig = GetContainerConfig(database);
                
                // Add environment variables to the log (sanitize sensitive data)
                foreach (var env in existingContainerConfig.Env)
                {
                    var envParts = env.Split('=', 2);
                    if (envParts.Length == 2)
                    {
                        var key = envParts[0];
                        var value = envParts[1];
                        
                        // Mask sensitive information like passwords in logs
                        if (key.Contains("PASSWORD", StringComparison.OrdinalIgnoreCase))
                        {
                            value = "*****";
                        }
                        
                        existingConfigLog.EnvironmentVariables[key] = value;
                    }
                }
                
                _loggingService.LogContainerConfig(existingConfigLog, database.Name);
                
                // Start streaming logs for this container
                await _loggingService.StartContainerLogStream(existingContainer.ID, database.Name);
                
                return existingContainer.ID;
            }

            // Create and start container based on database type
            _loggingService.LogInfo("Creating new container", database.Name);
            var containerConfig = GetContainerConfig(database);
            var hostConfig = GetHostConfig(database);
            
            // Create config log object to store detailed container configuration
            var configLog = new ContainerConfigLog
            {
                DatabaseName = database.Name,
                DatabaseType = database.Name.ToLower(),
                ContainerName = containerName,
                ImageName = imageName,
                ImageTag = imageTag,
                Port = database.Port,
                VolumePath = database.VolumePath,
                ConnectionString = database.ConnectionString
            };
            
            // Add environment variables to the log (sanitize sensitive data)
            foreach (var env in containerConfig.Env)
            {
                var configParts = env.Split('=', 2);
                if (configParts.Length == 2)
                {
                    var key = configParts[0];
                    var value = configParts[1];
                    
                    // Mask sensitive information like passwords in logs
                    if (key.Contains("PASSWORD", StringComparison.OrdinalIgnoreCase))
                    {
                        value = "*****";
                    }
                    
                    configLog.EnvironmentVariables[key] = value;
                }
            }
            
            // Add volume container path if applicable
            if (!string.IsNullOrEmpty(database.VolumePath))
            {
                configLog.VolumeContainerPath = database.Name.ToLower() switch
                {
                    "mysql" => "/var/lib/mysql",
                    "mssql" => "/var/opt/mssql/data",
                    "postgresql" => "/var/lib/postgresql/data",
                    "redis" => "/data",
                    _ => "unknown"
                };
            }
            
            _loggingService.LogInfo($"Container configuration: {containerConfig.Image}, Port: {database.Port}", database.Name);
            var response = await _client.Containers.CreateContainerAsync(new CreateContainerParameters
            {
                Image = containerConfig.Image,
                Name = containerName,
                Env = containerConfig.Env,
                ExposedPorts = containerConfig.ExposedPorts,
                HostConfig = hostConfig
            });

            _loggingService.LogInfo($"Container created with ID {response.ID.Substring(0, 12)}", database.Name);
            await _client.Containers.StartContainerAsync(response.ID, new ContainerStartParameters());
            _loggingService.LogInfo("Container started successfully", database.Name);
            
            // Now that we have the container ID, update the config log and save it
            configLog.ContainerId = response.ID;
            _loggingService.LogContainerConfig(configLog, database.Name);
            
            // Start streaming logs for this container
            await _loggingService.StartContainerLogStream(response.ID, database.Name);
            
            return response.ID;
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Failed to start container: {ex.Message}", database.Name);
            throw;
        }
    }

    public async Task StopDatabaseContainer(string containerId, string databaseName = null)
    {
        try
        {
            _loggingService.LogInfo($"Stopping container {containerId.Substring(0, 12)}", databaseName);
            
            // Stop log streaming for this container
            if (!string.IsNullOrEmpty(databaseName))
            {
                _loggingService.StopContainerLogStream(databaseName);
            }
            
            await _client.Containers.StopContainerAsync(containerId, new ContainerStopParameters
            {
                WaitBeforeKillSeconds = 10
            });
            
            _loggingService.LogInfo("Container stopped successfully", databaseName);
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Failed to stop container: {ex.Message}", databaseName);
            throw;
        }
    }

    public async Task<string> GetContainerStatus(string containerId, string databaseName = null)
    {
        try
        {
            var containers = await _client.Containers.ListContainersAsync(
                new ContainersListParameters
                {
                    All = true,
                    Filters = new Dictionary<string, IDictionary<string, bool>>
                    {
                        {
                            "id", new Dictionary<string, bool>
                            {
                                { containerId, true }
                            }
                        }
                    }
                });

            var container = containers.FirstOrDefault();
            var status = container?.State ?? "Not Found";
            
            if (container != null && !string.IsNullOrEmpty(databaseName))
            {
                _loggingService.LogInfo($"Container status: {status}", databaseName);
            }
            
            return status;
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrEmpty(databaseName))
            {
                _loggingService.LogError($"Error getting container status: {ex.Message}", databaseName);
            }
            return "Error";
        }
    }

    private (string Image, List<string> Env, Dictionary<string, EmptyStruct> ExposedPorts) GetContainerConfig(DatabaseContainer database)
    {
        // Use Type property for identifying database type, not Name
        string dbType = database.Type?.ToLowerInvariant().Trim() ?? string.Empty;
        _loggingService.LogInfo($"Getting container config for database type: '{dbType}'", database.Name);
        
        return dbType switch
        {
            "mysql" => (
                $"mysql:{database.Version}",
                new List<string> { $"MYSQL_ROOT_PASSWORD={database.Password}" },
                new Dictionary<string, EmptyStruct> { { $"{database.Port}/tcp", new EmptyStruct() } }
            ),
            "mssql" => (
                $"mcr.microsoft.com/mssql/server:{database.Version}-latest",
                new List<string> { "ACCEPT_EULA=Y", $"SA_PASSWORD={database.Password}" },
                new Dictionary<string, EmptyStruct> { { $"{database.Port}/tcp", new EmptyStruct() } }
            ),
            "postgresql" => (
                $"postgres:{database.Version}",
                new List<string> { $"POSTGRES_PASSWORD={database.Password}" },
                new Dictionary<string, EmptyStruct> { { $"{database.Port}/tcp", new EmptyStruct() } }
            ),
            "redis" => (
                $"redis:{database.Version}",
                new List<string>(),
                new Dictionary<string, EmptyStruct> { { $"{database.Port}/tcp", new EmptyStruct() } }
            ),
            _ => throw new ArgumentException($"Unsupported database type: '{dbType}' for database '{database.Name}'")
        };
        
    }

    private HostConfig GetHostConfig(DatabaseContainer database)
    {
        var hostConfig = new HostConfig
        {
            PortBindings = new Dictionary<string, IList<PortBinding>>
            {
                {
                    $"{database.Port}/tcp",
                    new List<PortBinding>
                    {
                        new PortBinding
                        {
                            HostPort = database.Port.ToString()
                        }
                    }
                }
            }
        };

        // Add volume binding if path is specified
        if (!string.IsNullOrEmpty(database.VolumePath))
        {
            // Use Type property for determining container path, not Name
            string dbType = database.Type?.ToLowerInvariant().Trim() ?? string.Empty;
            _loggingService.LogInfo($"Getting host config for database type: '{dbType}'", database.Name);
            
            string containerPath = dbType switch
            {
                "mysql" => "/var/lib/mysql",
                "mssql" => "/var/opt/mssql/data",
                "postgresql" => "/var/lib/postgresql/data",
                "redis" => "/data",
                _ => throw new ArgumentException($"Unsupported database type: '{dbType}' for database '{database.Name}'")
            };

            hostConfig.Binds = new List<string>
            {
                $"{database.VolumePath}:{containerPath}"
            };
        }

        return hostConfig;
    }

    private async Task EnsureImageExistsAsync(string imageName, string imageTag, IProgress<string>? progressReporter = null)
    {
        // Check if image exists locally
        var images = await _client.Images.ListImagesAsync(new ImagesListParameters
        {
            Filters = new Dictionary<string, IDictionary<string, bool>>
            {
                {
                    "reference", new Dictionary<string, bool>
                    {
                        { $"{imageName}:{imageTag}", true }
                    }
                }
            }
        });

        if (!images.Any())
        {
            // Image not found, pull it
            progressReporter?.Report($"Pulling image {imageName}:{imageTag}...");
            
            await _client.Images.CreateImageAsync(
                new ImagesCreateParameters
                {
                    FromImage = imageName,
                    Tag = imageTag
                },
                null, // AuthConfig for private registries, null for public
                new Progress<JSONMessage>(jsonMessage =>
                {
                    if (progressReporter != null)
                    {
                        var id = jsonMessage.ID ?? "";
                        var status = jsonMessage.Status ?? "";
                        var detail = "";

                        if (jsonMessage.Progress != null)
                        {
                            if (jsonMessage.Progress.Total > 0)
                            {
                                detail = $"{jsonMessage.Progress.Current * 100 / jsonMessage.Progress.Total}% ({FormatBytes(jsonMessage.Progress.Current)}/{FormatBytes(jsonMessage.Progress.Total)})";
                            }
                            else if (jsonMessage.Progress.Current > 0)
                            {
                                detail = FormatBytes(jsonMessage.Progress.Current);
                            }
                        }
                        else if (!string.IsNullOrEmpty(jsonMessage.ProgressMessage))
                        {
                            detail = jsonMessage.ProgressMessage;
                        }

                        string reportMessage = "";
                        if (!string.IsNullOrEmpty(id)) reportMessage += $"{id}: ";
                        reportMessage += status;
                        if (!string.IsNullOrEmpty(detail)) reportMessage += $" {detail}";
                        
                        reportMessage = reportMessage.Trim();

                        bool isNoisy = (status == "Waiting" || status == "Buffering" || status == "Verifying Checksum") && string.IsNullOrEmpty(detail);
                        
                        if (!string.IsNullOrWhiteSpace(reportMessage) && !isNoisy)
                        {
                            progressReporter.Report(reportMessage);
                        }
                    }
                }));
            
            progressReporter?.Report($"Image {imageName}:{imageTag} pulled successfully");
        }
        else
        {
            progressReporter?.Report($"Image {imageName}:{imageTag} already exists locally");
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffix = { "B", "KB", "MB", "GB", "TB" };
        int i;
        double dblSByte = bytes;
        for (i = 0; i < suffix.Length && bytes >= 1024; i++, bytes /= 1024)
        {
            dblSByte = bytes / 1024.0;
        }
        return string.Format("{0:0.##} {1}", dblSByte, suffix[i]);
    }
}
