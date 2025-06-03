using System;
using System.Collections.Generic;
using System.Linq;

namespace DatabaseDock.Models
{
    /// <summary>
    /// Class for storing detailed container configuration information for logging purposes
    /// </summary>
    public class ContainerConfigLog
    {
        public string DatabaseName { get; set; }
        public string DatabaseType { get; set; }
        public string ContainerName { get; set; }
        public string ContainerId { get; set; }
        public string ImageName { get; set; }
        public string ImageTag { get; set; }
        public string VolumePath { get; set; }
        public string VolumeContainerPath { get; set; }
        public int Port { get; set; }
        public string ConnectionString { get; set; }
        public Dictionary<string, string> EnvironmentVariables { get; set; }
        public DateTime CreatedAt { get; set; }

        public ContainerConfigLog()
        {
            CreatedAt = DateTime.Now;
            EnvironmentVariables = new Dictionary<string, string>();
        }

        public override string ToString()
        {
            var envVars = string.Join(", ", EnvironmentVariables.Select(kv => $"{kv.Key}={kv.Value}"));
            
            return $"Container Configuration:\n" +
                   $"- Database: {DatabaseName} ({DatabaseType})\n" +
                   $"- Container: {ContainerName} ({ContainerId})\n" +
                   $"- Image: {ImageName}:{ImageTag}\n" +
                   $"- Port: {Port}\n" +
                   $"- Volume: {VolumePath} → {VolumeContainerPath}\n" +
                   $"- Environment: {envVars}\n" +
                   $"- Connection String: {ConnectionString}";
        }
    }
}
