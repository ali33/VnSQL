using VnSQL.Core.Models;
using VnSQL.Core.Interfaces;

namespace VnSQL.Core.Sql;

/// <summary>
/// Default implementation of protocol response formatter
/// </summary>
public class ProtocolResponseFormatter : IProtocolResponseFormatter
{
    public QueryResult FormatResponse(QueryResult result, string protocol)
    {
        if (result.ColumnTypes == null) return result;
        
        // Convert column types to protocol-specific types
        var protocolColumnTypes = result.ColumnTypes.Select(ct => GetProtocolColumnType(ct, protocol)).ToList();
        
        return new QueryResult
        {
            Success = result.Success,
            Data = result.Data,
            ColumnNames = result.ColumnNames,
            ColumnTypes = protocolColumnTypes,
            AffectedRows = result.AffectedRows,
            LastInsertId = result.LastInsertId,
            ErrorMessage = result.ErrorMessage
        };
    }
    
    public string GetProtocolColumnType(string columnType, string protocol)
    {
        return protocol.ToUpper() switch
        {
            "MYSQL" => GetMySQLColumnType(columnType),
            "POSTGRESQL" => GetPostgreSQLColumnType(columnType),
            "SQLITE" => GetSQLiteColumnType(columnType),
            _ => columnType
        };
    }
    
    public string FormatErrorMessage(int errorCode, string message, string protocol)
    {
        return protocol.ToUpper() switch
        {
            "MYSQL" => $"MySQL Error {errorCode}: {message}",
            "POSTGRESQL" => $"PostgreSQL Error {errorCode}: {message}",
            "SQLITE" => $"SQLite Error {errorCode}: {message}",
            _ => $"Error {errorCode}: {message}"
        };
    }
    
    private string GetMySQLColumnType(string columnType)
    {
        return columnType.ToUpper() switch
        {
            "VARCHAR" => "VARCHAR(255)",
            "INT" => "INT",
            "BIGINT" => "BIGINT",
            "DECIMAL" => "DECIMAL(10,2)",
            "DATETIME" => "DATETIME",
            "BOOLEAN" => "TINYINT(1)",
            _ => columnType
        };
    }
    
    private string GetPostgreSQLColumnType(string columnType)
    {
        return columnType.ToUpper() switch
        {
            "VARCHAR" => "VARCHAR",
            "INT" => "INTEGER",
            "BIGINT" => "BIGINT",
            "DECIMAL" => "NUMERIC",
            "DATETIME" => "TIMESTAMP",
            "BOOLEAN" => "BOOLEAN",
            _ => columnType
        };
    }
    
    private string GetSQLiteColumnType(string columnType)
    {
        return columnType.ToUpper() switch
        {
            "VARCHAR" => "TEXT",
            "INT" => "INTEGER",
            "BIGINT" => "INTEGER",
            "DECIMAL" => "REAL",
            "DATETIME" => "TEXT",
            "BOOLEAN" => "INTEGER",
            _ => columnType
        };
    }
}
