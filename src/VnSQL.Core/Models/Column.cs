namespace VnSQL.Core.Models;

/// <summary>
/// Đại diện cho một cột trong bảng
/// </summary>
public class Column
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public int? Length { get; set; }
    public bool IsNullable { get; set; } = true;
    public bool IsPrimaryKey { get; set; } = false;
    public bool IsAutoIncrement { get; set; } = false;
    public string? DefaultValue { get; set; }
    public string? Comment { get; set; }
    public string Charset { get; set; } = "utf8mb4";
    public string Collation { get; set; } = "utf8mb4_unicode_ci";
    
    public Column()
    {
    }
    
    public Column(string name, string dataType)
    {
        Name = name;
        DataType = dataType;
    }
    
    public Column(string name, string dataType, int length)
    {
        Name = name;
        DataType = dataType;
        Length = length;
    }
    
    public string GetFullDataType()
    {
        if (Length.HasValue)
        {
            return $"{DataType}({Length})";
        }
        return DataType;
    }
    
    public override string ToString()
    {
        return $"{Name} {GetFullDataType()}" +
               (IsNullable ? "" : " NOT NULL") +
               (IsPrimaryKey ? " PRIMARY KEY" : "") +
               (IsAutoIncrement ? " AUTO_INCREMENT" : "") +
               (DefaultValue != null ? $" DEFAULT {DefaultValue}" : "");
    }
}
