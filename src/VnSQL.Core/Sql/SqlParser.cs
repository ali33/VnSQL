using System.Text.RegularExpressions;

namespace VnSQL.Core.Sql;

/// <summary>
/// Simple SQL Parser for VnSQL
/// </summary>
public class SqlParser
{
    // MySQL-style patterns (backticks)
    private static readonly Regex ShowDatabasesRegex = new(@"^SHOW\s+DATABASES\s*;?\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ShowTablesRegex = new(@"^SHOW\s+TABLES\s*;?\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex UseDatabaseRegex = new(@"^USE\s+[`""]?(\w+)[`""]?\s*;?\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CreateDatabaseRegex = new(@"^CREATE\s+DATABASE\s+(?:IF\s+NOT\s+EXISTS\s+)?[`""]?(\w+)[`""]?\s*;?\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DropDatabaseRegex = new(@"^DROP\s+DATABASE\s+(?:IF\s+EXISTS\s+)?[`""]?(\w+)[`""]?\s*;?\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CreateTableRegex = new(@"^CREATE\s+TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?[`""]?(\w+)[`""]?\s*\((.+)\)\s*;?\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex DropTableRegex = new(@"^DROP\s+TABLE\s+(?:IF\s+EXISTS\s+)?[`""]?(\w+)[`""]?\s*;?\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SelectRegex = new(@"^SELECT\s+(.+?)\s+FROM\s+[`""]?(\w+)[`""]?\s*(?:WHERE\s+(.+?))?\s*;?\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex InsertRegex = new(@"^INSERT\s+INTO\s+[`""]?(\w+)[`""]?\s*\((.+?)\)\s+VALUES\s*\((.+?)\)\s*;?\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex UpdateRegex = new(@"^UPDATE\s+[`""]?(\w+)[`""]?\s+SET\s+(.+?)\s*(?:WHERE\s+(.+?))?\s*;?\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DeleteRegex = new(@"^DELETE\s+FROM\s+[`""]?(\w+)[`""]?\s*(?:WHERE\s+(.+?))?\s*;?\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DescribeTableRegex = new(@"^DESCRIBE\s+[`""]?(\w+)[`""]?\s*;?\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    // PostgreSQL-style patterns (double quotes)
    private static readonly Regex PgListDatabasesRegex = new(@"^SELECT\s+datname\s+FROM\s+pg_database\s*;?\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PgListTablesRegex = new(@"^SELECT\s+tablename\s+FROM\s+pg_tables\s+WHERE\s+schemaname\s*=\s*'public'\s*;?\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PgDescribeTableRegex = new(@"^SELECT\s+column_name,\s*data_type,\s*is_nullable,\s*column_default\s+FROM\s+information_schema\.columns\s+WHERE\s+table_name\s*=\s*[`""]?(\w+)[`""]?\s*;?\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static SqlCommand Parse(string sql)
    {
        var trimmedSql = sql.Trim();
        
        // SHOW DATABASES (MySQL) or SELECT datname FROM pg_database (PostgreSQL)
        if (ShowDatabasesRegex.IsMatch(trimmedSql) || PgListDatabasesRegex.IsMatch(trimmedSql))
        {
            return new SqlCommand
            {
                Type = SqlCommandType.ShowDatabases,
                RawSql = trimmedSql
            };
        }
        
        // SHOW TABLES (MySQL) or SELECT tablename FROM pg_tables (PostgreSQL)
        if (ShowTablesRegex.IsMatch(trimmedSql) || PgListTablesRegex.IsMatch(trimmedSql))
        {
            return new SqlCommand
            {
                Type = SqlCommandType.ShowTables,
                RawSql = trimmedSql
            };
        }
        
        // USE DATABASE
        var useMatch = UseDatabaseRegex.Match(trimmedSql);
        if (useMatch.Success)
        {
            return new SqlCommand
            {
                Type = SqlCommandType.UseDatabase,
                DatabaseName = useMatch.Groups[1].Value,
                RawSql = trimmedSql
            };
        }
        
        // CREATE DATABASE
        var createDbMatch = CreateDatabaseRegex.Match(trimmedSql);
        if (createDbMatch.Success)
        {
            return new SqlCommand
            {
                Type = SqlCommandType.CreateDatabase,
                DatabaseName = createDbMatch.Groups[1].Value,
                RawSql = trimmedSql
            };
        }
        
        // DROP DATABASE
        var dropDbMatch = DropDatabaseRegex.Match(trimmedSql);
        if (dropDbMatch.Success)
        {
            return new SqlCommand
            {
                Type = SqlCommandType.DropDatabase,
                DatabaseName = dropDbMatch.Groups[1].Value,
                RawSql = trimmedSql
            };
        }
        
        // CREATE TABLE
        var createTableMatch = CreateTableRegex.Match(trimmedSql);
        if (createTableMatch.Success)
        {
            return new SqlCommand
            {
                Type = SqlCommandType.CreateTable,
                TableName = createTableMatch.Groups[1].Value,
                Columns = ParseColumnDefinitions(createTableMatch.Groups[2].Value),
                RawSql = trimmedSql
            };
        }
        
        // DROP TABLE
        var dropTableMatch = DropTableRegex.Match(trimmedSql);
        if (dropTableMatch.Success)
        {
            return new SqlCommand
            {
                Type = SqlCommandType.DropTable,
                TableName = dropTableMatch.Groups[1].Value,
                RawSql = trimmedSql
            };
        }
        
        // SELECT
        var selectMatch = SelectRegex.Match(trimmedSql);
        if (selectMatch.Success)
        {
            return new SqlCommand
            {
                Type = SqlCommandType.Select,
                ColumnNames = ParseColumnList(selectMatch.Groups[1].Value),
                TableName = selectMatch.Groups[2].Value,
                WhereClause = selectMatch.Groups[3].Success ? selectMatch.Groups[3].Value : null,
                RawSql = trimmedSql
            };
        }
        
        // INSERT
        var insertMatch = InsertRegex.Match(trimmedSql);
        if (insertMatch.Success)
        {
            return new SqlCommand
            {
                Type = SqlCommandType.Insert,
                TableName = insertMatch.Groups[1].Value,
                ColumnNames = ParseColumnList(insertMatch.Groups[2].Value),
                Values = ParseValueList(insertMatch.Groups[3].Value),
                RawSql = trimmedSql
            };
        }
        
        // UPDATE
        var updateMatch = UpdateRegex.Match(trimmedSql);
        if (updateMatch.Success)
        {
            return new SqlCommand
            {
                Type = SqlCommandType.Update,
                TableName = updateMatch.Groups[1].Value,
                SetClause = updateMatch.Groups[2].Value,
                WhereClause = updateMatch.Groups[3].Success ? updateMatch.Groups[3].Value : null,
                RawSql = trimmedSql
            };
        }
        
        // DELETE
        var deleteMatch = DeleteRegex.Match(trimmedSql);
        if (deleteMatch.Success)
        {
            return new SqlCommand
            {
                Type = SqlCommandType.Delete,
                TableName = deleteMatch.Groups[1].Value,
                WhereClause = deleteMatch.Groups[2].Success ? deleteMatch.Groups[2].Value : null,
                RawSql = trimmedSql
            };
        }
        
        // DESCRIBE TABLE (MySQL) or SELECT column info (PostgreSQL)
        var describeMatch = DescribeTableRegex.Match(trimmedSql);
        var pgDescribeMatch = PgDescribeTableRegex.Match(trimmedSql);
        if (describeMatch.Success)
        {
            return new SqlCommand
            {
                Type = SqlCommandType.DescribeTable,
                TableName = describeMatch.Groups[1].Value,
                RawSql = trimmedSql
            };
        }
        else if (pgDescribeMatch.Success)
        {
            return new SqlCommand
            {
                Type = SqlCommandType.DescribeTable,
                TableName = pgDescribeMatch.Groups[1].Value,
                RawSql = trimmedSql
            };
        }
        
        // Unknown command
        return new SqlCommand
        {
            Type = SqlCommandType.Unknown,
            RawSql = trimmedSql
        };
    }
    
    private static List<ColumnDefinition> ParseColumnDefinitions(string columnDefs)
    {
        var columns = new List<ColumnDefinition>();
        var parts = columnDefs.Split(',', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            var spaceIndex = trimmed.IndexOf(' ');
            if (spaceIndex > 0)
            {
                var name = trimmed.Substring(0, spaceIndex).Trim('`');
                var typeAndConstraints = trimmed.Substring(spaceIndex + 1).Trim();
                
                columns.Add(new ColumnDefinition
                {
                    Name = name,
                    Type = ParseColumnType(typeAndConstraints),
                    IsPrimaryKey = typeAndConstraints.ToUpper().Contains("PRIMARY KEY"),
                    IsNotNull = typeAndConstraints.ToUpper().Contains("NOT NULL")
                });
            }
        }
        
        return columns;
    }
    
    private static List<string> ParseColumnList(string columns)
    {
        return columns.Split(',')
            .Select(c => c.Trim().Trim('`'))
            .Where(c => !string.IsNullOrEmpty(c))
            .ToList();
    }
    
    private static List<string> ParseValueList(string values)
    {
        return values.Split(',')
            .Select(v => v.Trim().Trim('\''))
            .Where(v => !string.IsNullOrEmpty(v))
            .ToList();
    }
    
    private static string ParseColumnType(string typeAndConstraints)
    {
        var parts = typeAndConstraints.Split(' ');
        return parts[0].ToUpper();
    }
}

public class SqlCommand
{
    public SqlCommandType Type { get; set; }
    public string? DatabaseName { get; set; }
    public string? TableName { get; set; }
    public List<ColumnDefinition>? Columns { get; set; }
    public List<string>? ColumnNames { get; set; }
    public List<string>? Values { get; set; }
    public string? WhereClause { get; set; }
    public string? SetClause { get; set; }
    public string RawSql { get; set; } = string.Empty;
}

public class ColumnDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsPrimaryKey { get; set; }
    public bool IsNotNull { get; set; }
}

public enum SqlCommandType
{
    Unknown,
    ShowDatabases,
    ShowTables,
    UseDatabase,
    CreateDatabase,
    DropDatabase,
    CreateTable,
    DropTable,
    Select,
    Insert,
    Update,
    Delete,
    DescribeTable
}
