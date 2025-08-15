using System.Collections.Concurrent;

namespace VnSQL.Core.Models;

/// <summary>
/// Đại diện cho một database trong VnSQL
/// </summary>
public class Database
{
    public string Name { get; set; } = string.Empty;
    public string Charset { get; set; } = "utf8mb4";
    public string Collation { get; set; } = "utf8mb4_unicode_ci";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    private readonly ConcurrentDictionary<string, Table> _tables = new();
    
    public IReadOnlyDictionary<string, Table> Tables => _tables;
    
    public void AddTable(Table table)
    {
        _tables.TryAdd(table.Name, table);
        UpdatedAt = DateTime.UtcNow;
    }
    
    public bool RemoveTable(string tableName)
    {
        var removed = _tables.TryRemove(tableName, out _);
        if (removed)
        {
            UpdatedAt = DateTime.UtcNow;
        }
        return removed;
    }
    
    public Table? GetTable(string tableName)
    {
        _tables.TryGetValue(tableName, out var table);
        return table;
    }
    
    public bool TableExists(string tableName)
    {
        return _tables.ContainsKey(tableName);
    }
}
