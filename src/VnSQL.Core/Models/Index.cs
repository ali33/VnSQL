namespace VnSQL.Core.Models;

/// <summary>
/// Đại diện cho một index trong bảng
/// </summary>
public class Index
{
    public string Name { get; set; } = string.Empty;
    public IndexType Type { get; set; } = IndexType.Index;
    public List<string> Columns { get; set; } = new();
    public bool IsUnique { get; set; } = false;
    public string? Comment { get; set; }
    
    public Index()
    {
    }
    
    public Index(string name, params string[] columns)
    {
        Name = name;
        Columns.AddRange(columns);
    }
    
    public Index(string name, IndexType type, params string[] columns)
    {
        Name = name;
        Type = type;
        Columns.AddRange(columns);
    }
    
    public override string ToString()
    {
        var typeStr = Type switch
        {
            IndexType.Primary => "PRIMARY KEY",
            IndexType.Unique => "UNIQUE",
            IndexType.Index => IsUnique ? "UNIQUE INDEX" : "INDEX",
            _ => "INDEX"
        };
        
        return $"{typeStr} {Name} ({string.Join(", ", Columns)})";
    }
}

/// <summary>
/// Loại index
/// </summary>
public enum IndexType
{
    Index,
    Unique,
    Primary
}
