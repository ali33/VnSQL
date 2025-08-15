using VnSQL.Core.Interfaces;
using VnSQL.Core.Models;
using Microsoft.Extensions.Logging;

namespace VnSQL.Core.Sql;

/// <summary>
/// Query Executor for VnSQL
/// </summary>
public class QueryExecutor
{
    private readonly IStorageEngine _storageEngine;
    private readonly ILogger<QueryExecutor> _logger;
    private readonly IProtocolResponseFormatter _responseFormatter;
    private string _currentDatabase = "default";
    private string _currentProtocol = "MySQL";

    public QueryExecutor(IStorageEngine storageEngine, ILogger<QueryExecutor> logger, IProtocolResponseFormatter responseFormatter)
    {
        _storageEngine = storageEngine;
        _logger = logger;
        _responseFormatter = responseFormatter;
    }

    public async Task<QueryResult> ExecuteAsync(string sql, string protocol = "MySQL")
    {
        try
        {
            _currentProtocol = protocol;
            _logger.LogInformation("Executing SQL: {Sql} for protocol: {Protocol}", sql, protocol);
            
            var command = SqlParser.Parse(sql);
            
            var result = command.Type switch
            {
                SqlCommandType.ShowDatabases => await ExecuteShowDatabasesAsync(),
                SqlCommandType.ShowTables => await ExecuteShowTablesAsync(),
                SqlCommandType.ShowEngines => await ExecuteShowEnginesAsync(),
                SqlCommandType.ShowVariables => await ExecuteShowVariablesAsync(),
                SqlCommandType.UseDatabase => await ExecuteUseDatabaseAsync(command.DatabaseName!),
                SqlCommandType.CreateDatabase => await ExecuteCreateDatabaseAsync(command.DatabaseName!),
                SqlCommandType.DropDatabase => await ExecuteDropDatabaseAsync(command.DatabaseName!),
                SqlCommandType.CreateTable => await ExecuteCreateTableAsync(command.TableName!, command.Columns!),
                SqlCommandType.DropTable => await ExecuteDropTableAsync(command.TableName!),
                SqlCommandType.AlterTable => await ExecuteAlterTableAsync(command.TableName!, command.AlterClause!),
                SqlCommandType.CreateIndex => await ExecuteCreateIndexAsync(command.IndexName!, command.TableName!, command.ColumnNames!),
                SqlCommandType.DropIndex => await ExecuteDropIndexAsync(command.IndexName!),
                SqlCommandType.Grant => await ExecuteGrantAsync(command.Privileges!, command.TableName!, command.UserName!),
                SqlCommandType.Revoke => await ExecuteRevokeAsync(command.Privileges!, command.TableName!, command.UserName!),
                SqlCommandType.Select => await ExecuteSelectAsync(command),
                SqlCommandType.Insert => await ExecuteInsertAsync(command),
                SqlCommandType.Update => await ExecuteUpdateAsync(command),
                SqlCommandType.Delete => await ExecuteDeleteAsync(command),
                SqlCommandType.DescribeTable => await ExecuteDescribeTableAsync(command.TableName!),
                _ => new QueryResult { Success = false, ErrorMessage = $"Unsupported command: {sql}" }
            };
            
            // Format response for the specific protocol
            return _responseFormatter.FormatResponse(result, protocol);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing SQL: {Sql}", sql);
            var errorMessage = _responseFormatter.FormatErrorMessage(1064, ex.Message, protocol);
            return new QueryResult { Success = false, ErrorMessage = errorMessage };
        }
    }

    private async Task<QueryResult> ExecuteShowDatabasesAsync()
    {
        var databaseNames = await _storageEngine.ListDatabasesAsync();
        var data = databaseNames.Select(dbName => new Dictionary<string, object?> { ["Database"] = dbName }).ToList();
        
        return new QueryResult
        {
            Success = true,
            Data = data,
            ColumnNames = new List<string> { "Database" },
            ColumnTypes = new List<string> { "VARCHAR" },
            AffectedRows = data.Count
        };
    }

    private async Task<QueryResult> ExecuteShowTablesAsync()
    {
        var database = await _storageEngine.LoadDatabaseAsync(_currentDatabase);
        if (database == null)
        {
            return new QueryResult { Success = false, ErrorMessage = $"Database '{_currentDatabase}' not found" };
        }

        var data = database.Tables.Values.Select(table => new Dictionary<string, object?> { ["Tables_in_" + _currentDatabase] = table.Name }).ToList();
        
        return new QueryResult
        {
            Success = true,
            Data = data,
            ColumnNames = new List<string> { $"Tables_in_{_currentDatabase}" },
            ColumnTypes = new List<string> { "VARCHAR" },
            AffectedRows = data.Count
        };
    }

    private async Task<QueryResult> ExecuteUseDatabaseAsync(string databaseName)
    {
        var exists = await _storageEngine.DatabaseExistsAsync(databaseName);
        if (!exists)
        {
            return new QueryResult { Success = false, ErrorMessage = $"Database '{databaseName}' not found" };
        }

        _currentDatabase = databaseName;
        
        return new QueryResult
        {
            Success = true,
            AffectedRows = 0,
            LastInsertId = 0
        };
    }

    private async Task<QueryResult> ExecuteCreateDatabaseAsync(string databaseName)
    {
        var exists = await _storageEngine.DatabaseExistsAsync(databaseName);
        if (exists)
        {
            return new QueryResult { Success = false, ErrorMessage = $"Database '{databaseName}' already exists" };
        }

        var database = new Database { Name = databaseName };
        await _storageEngine.SaveDatabaseAsync(database);
        
        return new QueryResult
        {
            Success = true,
            AffectedRows = 1,
            LastInsertId = 0
        };
    }

    private async Task<QueryResult> ExecuteDropDatabaseAsync(string databaseName)
    {
        var exists = await _storageEngine.DatabaseExistsAsync(databaseName);
        if (!exists)
        {
            return new QueryResult { Success = false, ErrorMessage = $"Database '{databaseName}' not found" };
        }

        await _storageEngine.DeleteDatabaseAsync(databaseName);
        
        if (_currentDatabase == databaseName)
        {
            _currentDatabase = "default";
        }
        
        return new QueryResult
        {
            Success = true,
            AffectedRows = 1,
            LastInsertId = 0
        };
    }

    private async Task<QueryResult> ExecuteCreateTableAsync(string tableName, List<ColumnDefinition> columns)
    {
        var database = await _storageEngine.LoadDatabaseAsync(_currentDatabase);
        if (database == null)
        {
            return new QueryResult { Success = false, ErrorMessage = $"Database '{_currentDatabase}' not found" };
        }

        if (database.TableExists(tableName))
        {
            return new QueryResult { Success = false, ErrorMessage = $"Table '{tableName}' already exists" };
        }

        var table = new Table { Name = tableName };
        
        foreach (var colDef in columns)
        {
            var column = new Column
            {
                Name = colDef.Name,
                DataType = colDef.Type,
                IsPrimaryKey = colDef.IsPrimaryKey,
                IsNullable = !colDef.IsNotNull
            };
            table.AddColumn(column);
        }

        database.AddTable(table);
        await _storageEngine.SaveDatabaseAsync(database);
        
        return new QueryResult
        {
            Success = true,
            AffectedRows = 0,
            LastInsertId = 0
        };
    }

    private async Task<QueryResult> ExecuteDropTableAsync(string tableName)
    {
        var database = await _storageEngine.LoadDatabaseAsync(_currentDatabase);
        if (database == null)
        {
            return new QueryResult { Success = false, ErrorMessage = $"Database '{_currentDatabase}' not found" };
        }

        if (!database.TableExists(tableName))
        {
            return new QueryResult { Success = false, ErrorMessage = $"Table '{tableName}' not found" };
        }

        database.RemoveTable(tableName);
        await _storageEngine.SaveDatabaseAsync(database);
        
        return new QueryResult
        {
            Success = true,
            AffectedRows = 1,
            LastInsertId = 0
        };
    }

    private async Task<QueryResult> ExecuteSelectAsync(SqlCommand command)
    {
        var database = await _storageEngine.LoadDatabaseAsync(_currentDatabase);
        if (database == null)
        {
            return new QueryResult { Success = false, ErrorMessage = $"Database '{_currentDatabase}' not found" };
        }

        var table = database.GetTable(command.TableName!);
        if (table == null)
        {
            return new QueryResult { Success = false, ErrorMessage = $"Table '{command.TableName}' not found" };
        }

        var data = new List<Dictionary<string, object?>>();
        var columnNames = command.ColumnNames ?? table.Columns.Select(c => c.Name).ToList();
        var columnTypes = table.Columns.Select(c => c.DataType).ToList();

        foreach (var row in table.Rows)
        {
            var rowData = new Dictionary<string, object?>();
            foreach (var columnName in columnNames)
            {
                var value = row.GetValue(columnName);
                rowData[columnName] = value;
            }
            data.Add(rowData);
        }

        return new QueryResult
        {
            Success = true,
            Data = data,
            ColumnNames = columnNames,
            ColumnTypes = columnTypes,
            AffectedRows = data.Count
        };
    }

    private async Task<QueryResult> ExecuteInsertAsync(SqlCommand command)
    {
        var database = await _storageEngine.LoadDatabaseAsync(_currentDatabase);
        if (database == null)
        {
            return new QueryResult { Success = false, ErrorMessage = $"Database '{_currentDatabase}' not found" };
        }

        var table = database.GetTable(command.TableName!);
        if (table == null)
        {
            return new QueryResult { Success = false, ErrorMessage = $"Table '{command.TableName}' not found" };
        }

        if (command.ColumnNames?.Count != command.Values?.Count)
        {
            return new QueryResult { Success = false, ErrorMessage = "Column count doesn't match value count" };
        }

        var row = new Row();

        for (int i = 0; i < command.ColumnNames!.Count; i++)
        {
            row.SetValue(command.ColumnNames[i], command.Values![i]);
        }

        table.AddRow(row);
        await _storageEngine.SaveDatabaseAsync(database);
        
        return new QueryResult
        {
            Success = true,
            AffectedRows = 1,
            LastInsertId = table.Rows.Count
        };
    }

    private async Task<QueryResult> ExecuteUpdateAsync(SqlCommand command)
    {
        var database = await _storageEngine.LoadDatabaseAsync(_currentDatabase);
        if (database == null)
        {
            return new QueryResult { Success = false, ErrorMessage = $"Database '{_currentDatabase}' not found" };
        }

        var table = database.GetTable(command.TableName!);
        if (table == null)
        {
            return new QueryResult { Success = false, ErrorMessage = $"Table '{command.TableName}' not found" };
        }

        // Simple update implementation - update all rows
        var affectedRows = 0;
        foreach (var row in table.Rows)
        {
            // Parse SET clause (simple implementation)
            var setPairs = command.SetClause!.Split(',');
            foreach (var pair in setPairs)
            {
                var parts = pair.Split('=');
                if (parts.Length == 2)
                {
                    var columnName = parts[0].Trim();
                    var value = parts[1].Trim().Trim('\'');
                    
                    row.SetValue(columnName, value);
                }
            }
            affectedRows++;
        }

        await _storageEngine.SaveDatabaseAsync(database);
        
        return new QueryResult
        {
            Success = true,
            AffectedRows = affectedRows,
            LastInsertId = 0
        };
    }

    private async Task<QueryResult> ExecuteDeleteAsync(SqlCommand command)
    {
        var database = await _storageEngine.LoadDatabaseAsync(_currentDatabase);
        if (database == null)
        {
            return new QueryResult { Success = false, ErrorMessage = $"Database '{_currentDatabase}' not found" };
        }

        var table = database.GetTable(command.TableName!);
        if (table == null)
        {
            return new QueryResult { Success = false, ErrorMessage = $"Table '{command.TableName}' not found" };
        }

        // Simple delete implementation - delete all rows
        var affectedRows = table.Rows.Count;
        // Note: This is a simplified implementation. In a real system, you'd need to implement proper row deletion
        
        await _storageEngine.SaveDatabaseAsync(database);
        
        return new QueryResult
        {
            Success = true,
            AffectedRows = affectedRows,
            LastInsertId = 0
        };
    }

    private async Task<QueryResult> ExecuteDescribeTableAsync(string tableName)
    {
        var database = await _storageEngine.LoadDatabaseAsync(_currentDatabase);
        if (database == null)
        {
            return new QueryResult { Success = false, ErrorMessage = $"Database '{_currentDatabase}' not found" };
        }

        var table = database.GetTable(tableName);
        if (table == null)
        {
            return new QueryResult { Success = false, ErrorMessage = $"Table '{tableName}' not found" };
        }

        var data = table.Columns.Select(col => new Dictionary<string, object?>
        {
            ["Field"] = col.Name,
            ["Type"] = col.DataType,
            ["Null"] = col.IsNullable ? "YES" : "NO",
            ["Key"] = col.IsPrimaryKey ? "PRI" : "",
            ["Default"] = col.DefaultValue ?? "",
            ["Extra"] = col.IsAutoIncrement ? "auto_increment" : ""
        }).ToList();

        return new QueryResult
        {
            Success = true,
            Data = data,
            ColumnNames = new List<string> { "Field", "Type", "Null", "Key", "Default", "Extra" },
            ColumnTypes = new List<string> { "VARCHAR", "VARCHAR", "VARCHAR", "VARCHAR", "VARCHAR", "VARCHAR" },
            AffectedRows = data.Count
        };
    }

    // New methods for advanced SQL commands
    private async Task<QueryResult> ExecuteShowEnginesAsync()
    {
        var data = new List<Dictionary<string, object?>>
        {
            new() { ["Engine"] = "InnoDB", ["Support"] = "DEFAULT", ["Comment"] = "Supports transactions, row-level locking, and foreign keys" },
            new() { ["Engine"] = "MyISAM", ["Support"] = "YES", ["Comment"] = "MyISAM storage engine" },
            new() { ["Engine"] = "Memory", ["Support"] = "YES", ["Comment"] = "Hash based, stored in memory, useful for temporary tables" }
        };

        return new QueryResult
        {
            Success = true,
            Data = data,
            ColumnNames = new List<string> { "Engine", "Support", "Comment" },
            ColumnTypes = new List<string> { "VARCHAR", "VARCHAR", "VARCHAR" },
            AffectedRows = data.Count
        };
    }

    private async Task<QueryResult> ExecuteShowVariablesAsync()
    {
        var data = new List<Dictionary<string, object?>>
        {
            new() { ["Variable_name"] = "version", ["Value"] = "5.7.28-VnSQL-1.0.0" },
            new() { ["Variable_name"] = "autocommit", ["Value"] = "ON" },
            new() { ["Variable_name"] = "sql_mode", ["Value"] = "STRICT_TRANS_TABLES,NO_ZERO_DATE,NO_ZERO_IN_DATE,ERROR_FOR_DIVISION_BY_ZERO" },
            new() { ["Variable_name"] = "character_set_database", ["Value"] = "utf8mb4" },
            new() { ["Variable_name"] = "collation_database", ["Value"] = "utf8mb4_unicode_ci" }
        };

        return new QueryResult
        {
            Success = true,
            Data = data,
            ColumnNames = new List<string> { "Variable_name", "Value" },
            ColumnTypes = new List<string> { "VARCHAR", "VARCHAR" },
            AffectedRows = data.Count
        };
    }

    private async Task<QueryResult> ExecuteAlterTableAsync(string tableName, string alterClause)
    {
        var database = await _storageEngine.LoadDatabaseAsync(_currentDatabase);
        if (database == null)
        {
            return new QueryResult { Success = false, ErrorMessage = $"Database '{_currentDatabase}' not found" };
        }

        var table = database.GetTable(tableName);
        if (table == null)
        {
            return new QueryResult { Success = false, ErrorMessage = $"Table '{tableName}' not found" };
        }

        // Simple ALTER TABLE implementation
        // In a real system, you'd need to parse the alter clause more thoroughly
        if (alterClause.ToUpper().Contains("ADD COLUMN"))
        {
            // Extract column definition from ALTER clause
            var columnMatch = System.Text.RegularExpressions.Regex.Match(alterClause, @"ADD\s+COLUMN\s+(\w+)\s+(\w+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (columnMatch.Success)
            {
                var columnName = columnMatch.Groups[1].Value;
                var columnType = columnMatch.Groups[2].Value;

                var column = new Column
                {
                    Name = columnName,
                    DataType = columnType,
                    IsNullable = true
                };

                table.AddColumn(column);
                await _storageEngine.SaveDatabaseAsync(database);

                return new QueryResult
                {
                    Success = true,
                    AffectedRows = 0,
                    LastInsertId = 0
                };
            }
        }

        return new QueryResult { Success = false, ErrorMessage = $"Unsupported ALTER TABLE operation: {alterClause}" };
    }

    private async Task<QueryResult> ExecuteCreateIndexAsync(string indexName, string tableName, List<string> columnNames)
    {
        var database = await _storageEngine.LoadDatabaseAsync(_currentDatabase);
        if (database == null)
        {
            return new QueryResult { Success = false, ErrorMessage = $"Database '{_currentDatabase}' not found" };
        }

        var table = database.GetTable(tableName);
        if (table == null)
        {
            return new QueryResult { Success = false, ErrorMessage = $"Table '{tableName}' not found" };
        }

        // Create index (simplified implementation)
        var index = new VnSQL.Core.Models.Index
        {
            Name = indexName,
            Columns = columnNames,
            IsUnique = false
        };

        table.AddIndex(index);
        await _storageEngine.SaveDatabaseAsync(database);

        return new QueryResult
        {
            Success = true,
            AffectedRows = 0,
            LastInsertId = 0
        };
    }

    private async Task<QueryResult> ExecuteDropIndexAsync(string indexName)
    {
        // Simplified implementation - would need to find the table containing this index
        return new QueryResult
        {
            Success = true,
            AffectedRows = 0,
            LastInsertId = 0
        };
    }

    private async Task<QueryResult> ExecuteGrantAsync(string privileges, string tableName, string userName)
    {
        // Simplified GRANT implementation
        return new QueryResult
        {
            Success = true,
            AffectedRows = 0,
            LastInsertId = 0
        };
    }

    private async Task<QueryResult> ExecuteRevokeAsync(string privileges, string tableName, string userName)
    {
        // Simplified REVOKE implementation
        return new QueryResult
        {
            Success = true,
            AffectedRows = 0,
            LastInsertId = 0
        };
    }
}
