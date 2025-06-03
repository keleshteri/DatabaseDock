using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using Npgsql;
using DatabaseDock.Models;
using StackExchange.Redis;

namespace DatabaseDock.Services
{
    public class DatabaseConnectionService
    {
        private readonly LoggingService _loggingService;

        public DatabaseConnectionService(LoggingService loggingService)
        {
            _loggingService = loggingService;
        }

        public async Task<(bool Success, string Message)> TestConnectionAsync(DatabaseContainer? database)
        {
            if (database == null)
            {
                return (false, "Database information is null");
            }

            _loggingService.LogInfo($"Testing connection to {database.Name}...", database.Name);
            _loggingService.LogInfo($"Database type: '{database.Type}', Name: '{database.Name}'", database.Name);

            try
            {
                // Normalize the database type to lowercase and trim any whitespace
                string dbType = database.Type?.ToLowerInvariant().Trim() ?? string.Empty;
                
                switch (dbType)
                {
                    case "mysql":
                        _loggingService.LogInfo("Testing MySQL connection", database.Name);
                        return await TestMySqlConnectionAsync(database);
                    case "mssql":
                        _loggingService.LogInfo("Testing MSSQL connection", database.Name);
                        return await TestMsSqlConnectionAsync(database);
                    case "postgresql":
                        _loggingService.LogInfo("Testing PostgreSQL connection", database.Name);
                        return await TestPostgreSqlConnectionAsync(database);
                    case "redis":
                        _loggingService.LogInfo("Testing Redis connection", database.Name);
                        return await TestRedisConnectionAsync(database);
                    default:
                        var errorMsg = $"Unsupported database type: '{database.Type}'"; 
                        _loggingService.LogError(errorMsg, database.Name);
                        return (false, errorMsg);
                }
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error testing connection: {ex.Message}";
                _loggingService.LogError(errorMsg, database.Name);
                return (false, errorMsg);
            }
        }

        private async Task<(bool Success, string Message)> TestMySqlConnectionAsync(DatabaseContainer database)
        {
            try
            {
                using (var connection = new MySqlConnection(database.ConnectionString))
                {
                    await connection.OpenAsync();
                    
                    // Perform a simple query to verify the connection
                    using (var command = new MySqlCommand("SELECT VERSION()", connection))
                    {
                        var version = await command.ExecuteScalarAsync();
                        var successMsg = $"Successfully connected to MySQL. Server version: {version}";
                        _loggingService.LogInfo(successMsg, database.Name);
                        return (true, successMsg);
                    }
                }
            }
            catch (Exception ex)
            {
                var errorMsg = $"MySQL connection failed: {ex.Message}";
                _loggingService.LogError(errorMsg, database.Name);
                return (false, errorMsg);
            }
        }

        [Obsolete]
        private async Task<(bool Success, string Message)> TestMsSqlConnectionAsync(DatabaseContainer database)
        {
            try
            {
                using (var connection = new SqlConnection(database.ConnectionString))
                {
                    await connection.OpenAsync();
                    
                    // Perform a simple query to verify the connection
                    using (var command = new SqlCommand("SELECT @@VERSION", connection))
                    {
                        var version = await command.ExecuteScalarAsync();
                        var successMsg = $"Successfully connected to SQL Server. Server version: {version}";
                        _loggingService.LogInfo(successMsg, database.Name);
                        return (true, successMsg);
                    }
                }
            }
            catch (Exception ex)
            {
                var errorMsg = $"SQL Server connection failed: {ex.Message}";
                _loggingService.LogError(errorMsg, database.Name);
                return (false, errorMsg);
            }
        }

        private async Task<(bool Success, string Message)> TestPostgreSqlConnectionAsync(DatabaseContainer database)
        {
            try
            {
                using (var connection = new NpgsqlConnection(database.ConnectionString))
                {
                    await connection.OpenAsync();
                    
                    // Perform a simple query to verify the connection
                    using (var command = new NpgsqlCommand("SELECT version()", connection))
                    {
                        var version = await command.ExecuteScalarAsync();
                        var successMsg = $"Successfully connected to PostgreSQL. Server version: {version}";
                        _loggingService.LogInfo(successMsg, database.Name);
                        return (true, successMsg);
                    }
                }
            }
            catch (Exception ex)
            {
                var errorMsg = $"PostgreSQL connection failed: {ex.Message}";
                _loggingService.LogError(errorMsg, database.Name);
                return (false, errorMsg);
            }
        }

        private async Task<(bool Success, string Message)> TestRedisConnectionAsync(DatabaseContainer database)
        {
            try
            {
                var connection = ConnectionMultiplexer.Connect(database.ConnectionString);
                var db = connection.GetDatabase();
                
                // Perform a simple PING command to verify the connection
                var pingResult = await db.PingAsync();
                
                var successMsg = $"Successfully connected to Redis. Ping: {pingResult.TotalMilliseconds}ms";
                _loggingService.LogInfo(successMsg, database.Name);
                
                connection.Close();
                return (true, successMsg);
            }
            catch (Exception ex)
            {
                var errorMsg = $"Redis connection failed: {ex.Message}";
                _loggingService.LogError(errorMsg, database.Name);
                return (false, errorMsg);
            }
        }
    }
}
