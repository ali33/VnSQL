using System.Collections.Concurrent;

namespace VnSQL.Core.Models;

/// <summary>
/// Đại diện cho một dòng dữ liệu trong bảng
/// </summary>
public class Row
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    private readonly ConcurrentDictionary<string, object?> _values = new();
    
    public IReadOnlyDictionary<string, object?> Values => _values;
    
    public Row()
    {
    }
    
    public Row(long id)
    {
        Id = id;
    }
    
    public void SetValue(string columnName, object? value)
    {
        _values.AddOrUpdate(columnName, value, (_, _) => value);
        UpdatedAt = DateTime.UtcNow;
    }
    
    public object? GetValue(string columnName)
    {
        _values.TryGetValue(columnName, out var value);
        return value;
    }
    
    public T? GetValue<T>(string columnName)
    {
        var value = GetValue(columnName);
        if (value is T typedValue)
        {
            return typedValue;
        }
        
        try
        {
            return (T?)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return default(T);
        }
    }
    
    public bool HasValue(string columnName)
    {
        return _values.ContainsKey(columnName);
    }
    
    public void RemoveValue(string columnName)
    {
        _values.TryRemove(columnName, out _);
        UpdatedAt = DateTime.UtcNow;
    }
    
    public override string ToString()
    {
        var values = string.Join(", ", _values.Select(kvp => $"{kvp.Key}={kvp.Value}"));
        return $"Row[{Id}]: {values}";
    }
}
