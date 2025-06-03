using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DatabaseDock.Models;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace DatabaseDock.Services
{
    public class DockerService
    {
        private readonly DockerClient _client;

        public DockerService()
        {
            // Connect to Docker daemon
            _client = new DockerClientConfiguration(new Uri("npipe://./pipe/docker_engine"))
                .CreateClient();
        }

        public async Task<bool> IsDockerRunning()
        {
            try
            {
                await _client.System.PingAsync();
                return true;
            }
            catch
            {
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

        public async Task<string> StartDatabaseContainer(DatabaseContainer database, IProgress<string> progressReporter = null)
        {
            // Get container configuration to extract image name and tag
            var (imageNameWithTag, _, _) = GetContainerConfig(database);
            var parts = imageNameWithTag.Split(':');
            string imageName = parts[0];
            string imageTag = parts.Length > 1 ? parts[1] : "latest";

            // Ensure the image exists locally, pull if necessary
            await EnsureImageExistsAsync(imageName, imageTag, progressReporter);

            // Ensure volume directory exists
            if (!string.IsNullOrEmpty(database.VolumePath) && !Directory.Exists(database.VolumePath))
            {
                Directory.CreateDirectory(database.VolumePath);
            }

            // Check if container already exists
            var existingContainers = await _client.Containers.ListContainersAsync(
                new ContainersListParameters
                {
                    All = true,
                    Filters = new Dictionary<string, IDictionary<string, bool>>
                    {
                        {
                            "name", new Dictionary<string, bool>
                            {
                                { $"databasedock-{database.Name.ToLower()}", true }
                            }
                        }
                    }
                });

            var existingContainer = existingContainers.FirstOrDefault();

            // If container exists, start it
            if (existingContainer != null)
            {
                await _client.Containers.StartContainerAsync(existingContainer.ID, new ContainerStartParameters());
                return existingContainer.ID;
            }

            // Create and start container based on database type
            var containerConfig = GetContainerConfig(database);
            var hostConfig = GetHostConfig(database);

            var response = await _client.Containers.CreateContainerAsync(new CreateContainerParameters
            {
                Image = containerConfig.Image,
                Name = $"databasedock-{database.Name.ToLower()}",
                Env = containerConfig.Env,
                ExposedPorts = containerConfig.ExposedPorts,
                HostConfig = hostConfig
            });

            await _client.Containers.StartContainerAsync(response.ID, new ContainerStartParameters());
            return response.ID;
        }

        public async Task StopDatabaseContainer(string containerId)
        {
            await _client.Containers.StopContainerAsync(containerId, new ContainerStopParameters
            {
                WaitBeforeKillSeconds = 10
            });
        }

        public async Task<string> GetContainerStatus(string containerId)
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
                return container?.State ?? "Not Found";
            }
            catch
            {
                return "Error";
            }
        }

        private (string Image, List<string> Env, Dictionary<string, EmptyStruct> ExposedPorts) GetContainerConfig(DatabaseContainer database)
        {
            return database.Name.ToLower() switch
            {
                "mysql" => (
                    $"mysql:{database.Version}",
                    new List<string> { "MYSQL_ROOT_PASSWORD=password" },
                    new Dictionary<string, EmptyStruct> { { $"{database.Port}/tcp", new EmptyStruct() } }
                ),
                "mssql" => (
                    $"mcr.microsoft.com/mssql/server:{database.Version}-latest",
                    new List<string> { "ACCEPT_EULA=Y", "SA_PASSWORD=P@ssw0rd" },
                    new Dictionary<string, EmptyStruct> { { $"{database.Port}/tcp", new EmptyStruct() } }
                ),
                "postgresql" => (
                    $"postgres:{database.Version}",
                    new List<string> { "POSTGRES_PASSWORD=postgres" },
                    new Dictionary<string, EmptyStruct> { { $"{database.Port}/tcp", new EmptyStruct() } }
                ),
                "redis" => (
                    $"redis:{database.Version}",
                    new List<string>(),
                    new Dictionary<string, EmptyStruct> { { $"{database.Port}/tcp", new EmptyStruct() } }
                ),
                _ => throw new ArgumentException($"Unsupported database type: {database.Name}")
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
                string containerPath = database.Name.ToLower() switch
                {
                    "mysql" => "/var/lib/mysql",
                    "mssql" => "/var/opt/mssql/data",
                    "postgresql" => "/var/lib/postgresql/data",
                    "redis" => "/data",
                    _ => throw new ArgumentException($"Unsupported database type: {database.Name}")
                };

                hostConfig.Binds = new List<string>
                {
                    $"{database.VolumePath}:{containerPath}"
                };
            }

            return hostConfig;
        }

        private async Task EnsureImageExistsAsync(string imageName, string imageTag, IProgress<string> progressReporter = null)
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
}
