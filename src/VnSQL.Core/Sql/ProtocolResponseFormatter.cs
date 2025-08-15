using VnSQL.Core.Models;
using VnSQL.Core.Interfaces;

namespace VnSQL.Core.Sql;

/// <summary>
/// Enhanced protocol response formatter supporting multiple database protocols
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
            "MARIADB" => GetMariaDBColumnType(columnType),
            "POSTGRESQL" => GetPostgreSQLColumnType(columnType),
            "SQLITE" => GetSQLiteColumnType(columnType),
            "ORACLE" => GetOracleColumnType(columnType),
            "SQLSERVER" => GetSQLServerColumnType(columnType),
            _ => columnType
        };
    }
    
    public string FormatErrorMessage(int errorCode, string message, string protocol)
    {
        return protocol.ToUpper() switch
        {
            "MYSQL" => $"MySQL Error {errorCode}: {message}",
            "MARIADB" => $"MariaDB Error {errorCode}: {message}",
            "POSTGRESQL" => $"PostgreSQL Error {errorCode}: {message}",
            "SQLITE" => $"SQLite Error {errorCode}: {message}",
            "ORACLE" => $"Oracle Error {errorCode}: {message}",
            "SQLSERVER" => $"SQL Server Error {errorCode}: {message}",
            _ => $"Error {errorCode}: {message}"
        };
    }
    
    private string GetMySQLColumnType(string columnType)
    {
        return columnType.ToUpper() switch
        {
            "VARCHAR" => "VARCHAR(255)",
            "CHAR" => "CHAR(1)",
            "TEXT" => "TEXT",
            "INT" => "INT",
            "INTEGER" => "INT",
            "BIGINT" => "BIGINT",
            "SMALLINT" => "SMALLINT",
            "TINYINT" => "TINYINT",
            "DECIMAL" => "DECIMAL(10,2)",
            "NUMERIC" => "DECIMAL(10,2)",
            "FLOAT" => "FLOAT",
            "DOUBLE" => "DOUBLE",
            "DATETIME" => "DATETIME",
            "TIMESTAMP" => "TIMESTAMP",
            "DATE" => "DATE",
            "TIME" => "TIME",
            "YEAR" => "YEAR",
            "BOOLEAN" => "TINYINT(1)",
            "BOOL" => "TINYINT(1)",
            "BLOB" => "BLOB",
            "LONGBLOB" => "LONGBLOB",
            "JSON" => "JSON",
            _ => columnType
        };
    }
    
    private string GetMariaDBColumnType(string columnType)
    {
        // MariaDB is mostly compatible with MySQL, but has some additional types
        return columnType.ToUpper() switch
        {
            "VARCHAR" => "VARCHAR(255)",
            "CHAR" => "CHAR(1)",
            "TEXT" => "TEXT",
            "INT" => "INT",
            "INTEGER" => "INT",
            "BIGINT" => "BIGINT",
            "SMALLINT" => "SMALLINT",
            "TINYINT" => "TINYINT",
            "DECIMAL" => "DECIMAL(10,2)",
            "NUMERIC" => "DECIMAL(10,2)",
            "FLOAT" => "FLOAT",
            "DOUBLE" => "DOUBLE",
            "DATETIME" => "DATETIME",
            "TIMESTAMP" => "TIMESTAMP",
            "DATE" => "DATE",
            "TIME" => "TIME",
            "YEAR" => "YEAR",
            "BOOLEAN" => "TINYINT(1)",
            "BOOL" => "TINYINT(1)",
            "BLOB" => "BLOB",
            "LONGBLOB" => "LONGBLOB",
            "JSON" => "JSON",
            "UUID" => "CHAR(36)", // MariaDB specific
            "INET4" => "INT UNSIGNED", // MariaDB specific
            "INET6" => "VARBINARY(16)", // MariaDB specific
            _ => columnType
        };
    }
    
    private string GetPostgreSQLColumnType(string columnType)
    {
        return columnType.ToUpper() switch
        {
            "VARCHAR" => "VARCHAR",
            "CHAR" => "CHAR",
            "TEXT" => "TEXT",
            "INT" => "INTEGER",
            "INTEGER" => "INTEGER",
            "BIGINT" => "BIGINT",
            "SMALLINT" => "SMALLINT",
            "DECIMAL" => "NUMERIC",
            "NUMERIC" => "NUMERIC",
            "FLOAT" => "REAL",
            "DOUBLE" => "DOUBLE PRECISION",
            "DATETIME" => "TIMESTAMP",
            "TIMESTAMP" => "TIMESTAMP",
            "DATE" => "DATE",
            "TIME" => "TIME",
            "BOOLEAN" => "BOOLEAN",
            "BOOL" => "BOOLEAN",
            "BLOB" => "BYTEA",
            "JSON" => "JSON",
            "UUID" => "UUID",
            "SERIAL" => "SERIAL",
            "BIGSERIAL" => "BIGSERIAL",
            _ => columnType
        };
    }
    
    private string GetSQLiteColumnType(string columnType)
    {
        return columnType.ToUpper() switch
        {
            "VARCHAR" => "TEXT",
            "CHAR" => "TEXT",
            "TEXT" => "TEXT",
            "INT" => "INTEGER",
            "INTEGER" => "INTEGER",
            "BIGINT" => "INTEGER",
            "SMALLINT" => "INTEGER",
            "TINYINT" => "INTEGER",
            "DECIMAL" => "REAL",
            "NUMERIC" => "REAL",
            "FLOAT" => "REAL",
            "DOUBLE" => "REAL",
            "DATETIME" => "TEXT",
            "TIMESTAMP" => "TEXT",
            "DATE" => "TEXT",
            "TIME" => "TEXT",
            "BOOLEAN" => "INTEGER",
            "BOOL" => "INTEGER",
            "BLOB" => "BLOB",
            "JSON" => "TEXT",
            _ => columnType
        };
    }
    
    private string GetOracleColumnType(string columnType)
    {
        return columnType.ToUpper() switch
        {
            "VARCHAR" => "VARCHAR2(255)",
            "CHAR" => "CHAR(1)",
            "TEXT" => "CLOB",
            "INT" => "NUMBER",
            "INTEGER" => "NUMBER",
            "BIGINT" => "NUMBER",
            "SMALLINT" => "NUMBER",
            "DECIMAL" => "NUMBER",
            "NUMERIC" => "NUMBER",
            "FLOAT" => "BINARY_FLOAT",
            "DOUBLE" => "BINARY_DOUBLE",
            "DATETIME" => "TIMESTAMP",
            "TIMESTAMP" => "TIMESTAMP",
            "DATE" => "DATE",
            "TIME" => "TIMESTAMP",
            "BOOLEAN" => "NUMBER(1)",
            "BOOL" => "NUMBER(1)",
            "BLOB" => "BLOB",
            "JSON" => "CLOB",
            _ => columnType
        };
    }
    
    private string GetSQLServerColumnType(string columnType)
    {
        return columnType.ToUpper() switch
        {
            "VARCHAR" => "VARCHAR(255)",
            "CHAR" => "CHAR(1)",
            "TEXT" => "TEXT",
            "INT" => "INT",
            "INTEGER" => "INT",
            "BIGINT" => "BIGINT",
            "SMALLINT" => "SMALLINT",
            "TINYINT" => "TINYINT",
            "DECIMAL" => "DECIMAL(10,2)",
            "NUMERIC" => "NUMERIC(10,2)",
            "FLOAT" => "FLOAT",
            "DOUBLE" => "FLOAT",
            "DATETIME" => "DATETIME",
            "TIMESTAMP" => "DATETIME2",
            "DATE" => "DATE",
            "TIME" => "TIME",
            "BOOLEAN" => "BIT",
            "BOOL" => "BIT",
            "BLOB" => "VARBINARY(MAX)",
            "JSON" => "NVARCHAR(MAX)",
            "UUID" => "UNIQUEIDENTIFIER",
            _ => columnType
        };
    }
}
