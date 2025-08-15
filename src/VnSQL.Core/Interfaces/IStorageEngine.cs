using VnSQL.Core.Models;

namespace VnSQL.Core.Interfaces;

/// <summary>
/// Interface cho storage engine
/// </summary>
public interface IStorageEngine
{
    /// <summary>
    /// Tên của storage engine
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Khởi tạo storage engine
    /// </summary>
    Task InitializeAsync();
    
    /// <summary>
    /// Lưu database
    /// </summary>
    Task SaveDatabaseAsync(Database database);
    
    /// <summary>
    /// Tải database
    /// </summary>
    Task<Database?> LoadDatabaseAsync(string databaseName);
    
    /// <summary>
    /// Xóa database
    /// </summary>
    Task DeleteDatabaseAsync(string databaseName);
    
    /// <summary>
    /// Lấy danh sách tất cả databases
    /// </summary>
    Task<IEnumerable<string>> ListDatabasesAsync();
    
    /// <summary>
    /// Kiểm tra database có tồn tại không
    /// </summary>
    Task<bool> DatabaseExistsAsync(string databaseName);
    
    /// <summary>
    /// Lưu table
    /// </summary>
    Task SaveTableAsync(string databaseName, Table table);
    
    /// <summary>
    /// Tải table
    /// </summary>
    Task<Table?> LoadTableAsync(string databaseName, string tableName);
    
    /// <summary>
    /// Xóa table
    /// </summary>
    Task DeleteTableAsync(string databaseName, string tableName);
    
    /// <summary>
    /// Lưu row
    /// </summary>
    Task SaveRowAsync(string databaseName, string tableName, Row row);
    
    /// <summary>
    /// Tải tất cả rows của table
    /// </summary>
    Task<IEnumerable<Row>> LoadRowsAsync(string databaseName, string tableName);
    
    /// <summary>
    /// Xóa row
    /// </summary>
    Task DeleteRowAsync(string databaseName, string tableName, long rowId);
    
    /// <summary>
    /// Backup dữ liệu
    /// </summary>
    Task BackupAsync(string backupPath);
    
    /// <summary>
    /// Restore dữ liệu
    /// </summary>
    Task RestoreAsync(string backupPath);
    
    /// <summary>
    /// Đóng storage engine
    /// </summary>
    Task CloseAsync();
}
