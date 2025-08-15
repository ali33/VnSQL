using Xunit;
using VnSQL.Core.Models;

namespace VnSQL.Tests.Core.Models;

public class DatabaseTests
{
    [Fact]
    public void Database_Creation_ShouldSetDefaultValues()
    {
        // Arrange & Act
        var database = new Database { Name = "testdb" };
        
        // Assert
        Assert.Equal("testdb", database.Name);
        Assert.Equal("utf8mb4", database.Charset);
        Assert.Equal("utf8mb4_unicode_ci", database.Collation);
        Assert.Empty(database.Tables);
    }
    
    [Fact]
    public void Database_AddTable_ShouldAddTableToList()
    {
        // Arrange
        var database = new Database { Name = "testdb" };
        var table = new Table { Name = "users" };
        
        // Act
        database.AddTable(table);
        
        // Assert
        Assert.Single(database.Tables);
        Assert.True(database.TableExists("users"));
        Assert.Equal(table, database.GetTable("users"));
    }
    
    [Fact]
    public void Database_RemoveTable_ShouldRemoveTableFromList()
    {
        // Arrange
        var database = new Database { Name = "testdb" };
        var table = new Table { Name = "users" };
        database.AddTable(table);
        
        // Act
        var result = database.RemoveTable("users");
        
        // Assert
        Assert.True(result);
        Assert.Empty(database.Tables);
        Assert.False(database.TableExists("users"));
        Assert.Null(database.GetTable("users"));
    }
    
    [Fact]
    public void Database_RemoveNonExistentTable_ShouldReturnFalse()
    {
        // Arrange
        var database = new Database { Name = "testdb" };
        
        // Act
        var result = database.RemoveTable("nonexistent");
        
        // Assert
        Assert.False(result);
    }
}
