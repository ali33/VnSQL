using System.Text.Json;
using VnSQL.Core.Interfaces;
using VnSQL.Core.Models;
using Microsoft.Extensions.Logging;

namespace VnSQL.Storage.Engines;

/// <summary>
/// File-based storage engine
/// </summary>
public class FileStorageEngine : IStorageEngine
{
    private readonly ILogger<FileStorageEngine> _logger;
    private readonly string _dataPath;
    private readonly JsonSerializerOptions _jsonOptions;
    
    public string Name => "FileStorage";
    
    public FileStorageEngine(ILogger<FileStorageEngine> logger, string dataPath = "./data")
    {
        _logger = logger;
        _dataPath = dataPath;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }
    
    public async Task InitializeAsync()
    {
        try
        {
            if (!Directory.Exists(_dataPath))
            {
                Directory.CreateDirectory(_dataPath);
                _logger.LogInformation("Created data directory: {DataPath}", _dataPath);
            }
            
            _logger.LogInformation("FileStorageEngine initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize FileStorageEngine");
            throw;
        }
    }
    
    public async Task SaveDatabaseAsync(Database database)
    {
        try
        {
            var dbPath = Path.Combine(_dataPath, database.Name);
            if (!Directory.Exists(dbPath))
            {
                Directory.CreateDirectory(dbPath);
            }
            
            var dbFile = Path.Combine(dbPath, "database.json");
            var json = JsonSerializer.Serialize(database, _jsonOptions);
            await File.WriteAllTextAsync(dbFile, json);
            
            _logger.LogDebug("Saved database: {DatabaseName}", database.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save database: {DatabaseName}", database.Name);
            throw;
        }
    }
    
    public async Task<Database?> LoadDatabaseAsync(string databaseName)
    {
        try
        {
            var dbFile = Path.Combine(_dataPath, databaseName, "database.json");
            if (!File.Exists(dbFile))
            {
                return null;
            }
            
            var json = await File.ReadAllTextAsync(dbFile);
            var database = JsonSerializer.Deserialize<Database>(json, _jsonOptions);
            
            _logger.LogDebug("Loaded database: {DatabaseName}", databaseName);
            return database;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load database: {DatabaseName}", databaseName);
            throw;
        }
    }
    
    public async Task DeleteDatabaseAsync(string databaseName)
    {
        try
        {
            var dbPath = Path.Combine(_dataPath, databaseName);
            if (Directory.Exists(dbPath))
            {
                Directory.Delete(dbPath, true);
                _logger.LogInformation("Deleted database: {DatabaseName}", databaseName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete database: {DatabaseName}", databaseName);
            throw;
        }
    }
    
    public async Task<IEnumerable<string>> ListDatabasesAsync()
    {
        try
        {
            if (!Directory.Exists(_dataPath))
            {
                return Enumerable.Empty<string>();
            }
            
            var databases = Directory.GetDirectories(_dataPath)
                .Where(dir => File.Exists(Path.Combine(dir, "database.json")))
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrEmpty(name))
                .ToList();
            
            _logger.LogDebug("Found {Count} databases", databases.Count);
            return databases;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list databases");
            throw;
        }
    }
    
    public async Task<bool> DatabaseExistsAsync(string databaseName)
    {
        var dbFile = Path.Combine(_dataPath, databaseName, "database.json");
        return File.Exists(dbFile);
    }
    
    public async Task SaveTableAsync(string databaseName, Table table)
    {
        try
        {
            var tablePath = Path.Combine(_dataPath, databaseName, "tables");
            if (!Directory.Exists(tablePath))
            {
                Directory.CreateDirectory(tablePath);
            }
            
            var tableFile = Path.Combine(tablePath, $"{table.Name}.json");
            var json = JsonSerializer.Serialize(table, _jsonOptions);
            await File.WriteAllTextAsync(tableFile, json);
            
            _logger.LogDebug("Saved table: {DatabaseName}.{TableName}", databaseName, table.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save table: {DatabaseName}.{TableName}", databaseName, table.Name);
            throw;
        }
    }
    
    public async Task<Table?> LoadTableAsync(string databaseName, string tableName)
    {
        try
        {
            var tableFile = Path.Combine(_dataPath, databaseName, "tables", $"{tableName}.json");
            if (!File.Exists(tableFile))
            {
                return null;
            }
            
            var json = await File.ReadAllTextAsync(tableFile);
            var table = JsonSerializer.Deserialize<Table>(json, _jsonOptions);
            
            _logger.LogDebug("Loaded table: {DatabaseName}.{TableName}", databaseName, tableName);
            return table;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load table: {DatabaseName}.{TableName}", databaseName, tableName);
            throw;
        }
    }
    
    public async Task DeleteTableAsync(string databaseName, string tableName)
    {
        try
        {
            var tableFile = Path.Combine(_dataPath, databaseName, "tables", $"{tableName}.json");
            if (File.Exists(tableFile))
            {
                File.Delete(tableFile);
                _logger.LogInformation("Deleted table: {DatabaseName}.{TableName}", databaseName, tableName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete table: {DatabaseName}.{TableName}", databaseName, tableName);
            throw;
        }
    }
    
    public async Task SaveRowAsync(string databaseName, string tableName, Row row)
    {
        try
        {
            var rowsPath = Path.Combine(_dataPath, databaseName, "tables", tableName, "rows");
            if (!Directory.Exists(rowsPath))
            {
                Directory.CreateDirectory(rowsPath);
            }
            
            var rowFile = Path.Combine(rowsPath, $"{row.Id}.json");
            var json = JsonSerializer.Serialize(row, _jsonOptions);
            await File.WriteAllTextAsync(rowFile, json);
            
            _logger.LogDebug("Saved row: {DatabaseName}.{TableName}.{RowId}", databaseName, tableName, row.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save row: {DatabaseName}.{TableName}.{RowId}", databaseName, tableName, row.Id);
            throw;
        }
    }
    
    public async Task<IEnumerable<Row>> LoadRowsAsync(string databaseName, string tableName)
    {
        try
        {
            var rowsPath = Path.Combine(_dataPath, databaseName, "tables", tableName, "rows");
            if (!Directory.Exists(rowsPath))
            {
                return Enumerable.Empty<Row>();
            }
            
            var rowFiles = Directory.GetFiles(rowsPath, "*.json");
            var rows = new List<Row>();
            
            foreach (var rowFile in rowFiles)
            {
                var json = await File.ReadAllTextAsync(rowFile);
                var row = JsonSerializer.Deserialize<Row>(json, _jsonOptions);
                if (row != null)
                {
                    rows.Add(row);
                }
            }
            
            _logger.LogDebug("Loaded {Count} rows from {DatabaseName}.{TableName}", rows.Count, databaseName, tableName);
            return rows;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load rows from {DatabaseName}.{TableName}", databaseName, tableName);
            throw;
        }
    }
    
    public async Task DeleteRowAsync(string databaseName, string tableName, long rowId)
    {
        try
        {
            var rowFile = Path.Combine(_dataPath, databaseName, "tables", tableName, "rows", $"{rowId}.json");
            if (File.Exists(rowFile))
            {
                File.Delete(rowFile);
                _logger.LogDebug("Deleted row: {DatabaseName}.{TableName}.{RowId}", databaseName, tableName, rowId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete row: {DatabaseName}.{TableName}.{RowId}", databaseName, tableName, rowId);
            throw;
        }
    }
    
    public async Task BackupAsync(string backupPath)
    {
        try
        {
            if (Directory.Exists(_dataPath))
            {
                var backupDir = Path.GetDirectoryName(backupPath);
                if (!string.IsNullOrEmpty(backupDir) && !Directory.Exists(backupDir))
                {
                    Directory.CreateDirectory(backupDir);
                }
                
                // Simple file copy backup
                if (Directory.Exists(backupPath))
                {
                    Directory.Delete(backupPath, true);
                }
                
                CopyDirectory(_dataPath, backupPath);
                _logger.LogInformation("Backup completed: {BackupPath}", backupPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create backup: {BackupPath}", backupPath);
            throw;
        }
    }
    
    public async Task RestoreAsync(string backupPath)
    {
        try
        {
            if (Directory.Exists(backupPath))
            {
                if (Directory.Exists(_dataPath))
                {
                    Directory.Delete(_dataPath, true);
                }
                
                CopyDirectory(backupPath, _dataPath);
                _logger.LogInformation("Restore completed from: {BackupPath}", backupPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore from backup: {BackupPath}", backupPath);
            throw;
        }
    }
    
    public async Task CloseAsync()
    {
        _logger.LogInformation("FileStorageEngine closed");
    }
    
    private static void CopyDirectory(string sourceDir, string destDir)
    {
        var dir = new DirectoryInfo(sourceDir);
        DirectoryInfo[] dirs = dir.GetDirectories();
        
        if (!Directory.Exists(destDir))
        {
            Directory.CreateDirectory(destDir);
        }
        
        foreach (FileInfo file in dir.GetFiles())
        {
            string targetFilePath = Path.Combine(destDir, file.Name);
            file.CopyTo(targetFilePath);
        }
        
        foreach (DirectoryInfo subDir in dirs)
        {
            string newDestinationDir = Path.Combine(destDir, subDir.Name);
            CopyDirectory(subDir.FullName, newDestinationDir);
        }
    }
}
