using System.Collections.Concurrent;

namespace VnSQL.Core.Models;

/// <summary>
/// Đại diện cho một bảng trong database
/// </summary>
public class Table
{
    public string Name { get; set; } = string.Empty;
    public string Engine { get; set; } = "InnoDB";
    public string Charset { get; set; } = "utf8mb4";
    public string Collation { get; set; } = "utf8mb4_unicode_ci";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    private readonly List<Column> _columns = new();
    private readonly List<Index> _indexes = new();
    private readonly ConcurrentQueue<Row> _rows = new();
    
    public IReadOnlyList<Column> Columns => _columns.AsReadOnly();
    public IReadOnlyList<Index> Indexes => _indexes.AsReadOnly();
    public IReadOnlyCollection<Row> Rows => _rows;
    
    public void AddColumn(Column column)
    {
        _columns.Add(column);
        UpdatedAt = DateTime.UtcNow;
    }
    
    public void AddIndex(Index index)
    {
        _indexes.Add(index);
        UpdatedAt = DateTime.UtcNow;
    }
    
    public void AddRow(Row row)
    {
        _rows.Enqueue(row);
        UpdatedAt = DateTime.UtcNow;
    }
    
    public void RemoveRow(Row row)
    {
        // Convert to list, remove, then recreate queue
        var rowsList = _rows.ToList();
        rowsList.Remove(row);
        _rows.Clear();
        foreach (var r in rowsList)
        {
            _rows.Enqueue(r);
        }
        UpdatedAt = DateTime.UtcNow;
    }
    
    public Column? GetColumn(string columnName)
    {
        return _columns.FirstOrDefault(c => c.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase));
    }
    
    public bool ColumnExists(string columnName)
    {
        return _columns.Any(c => c.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase));
    }
}
