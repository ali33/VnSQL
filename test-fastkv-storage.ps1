# Test FastKV Storage Engine
Write-Host "Testing FastKV Storage Engine..." -ForegroundColor Green

# Build the project
Write-Host "Building project..." -ForegroundColor Yellow
dotnet build

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "Build successful!" -ForegroundColor Green

# Create a simple test program
$testCode = @"
using System;
using System.Threading.Tasks;
using VnSQL.Storage.Engines;
using VnSQL.Core.Models;
using Microsoft.Extensions.Logging;

class FastKvTest
{
    static async Task Main()
    {
        Console.WriteLine("Testing FastKV Storage Engine...");
        
        // Create logger
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<FastKvStorageEngine>();
        
        // Create storage engine
        var storage = new FastKvStorageEngine(logger, "./test-fastkv-data", 2);
        
        try
        {
            // Initialize
            await storage.InitializeAsync();
            Console.WriteLine("Storage initialized successfully");
            
            // Create test database
            var db = new Database { Name = "testdb" };
            await storage.SaveDatabaseAsync(db);
            Console.WriteLine("Database saved");
            
            // Create test table
            var table = new Table { Name = "users" };
            table.AddColumn(new Column { Name = "id", DataType = "INT", IsNullable = false });
            table.AddColumn(new Column { Name = "name", DataType = "VARCHAR(255)", IsNullable = true });
            
            await storage.SaveTableAsync("testdb", table);
            Console.WriteLine("Table saved");
            
            // Create test rows
            for (int i = 1; i <= 5; i++)
            {
                var row = new Row { Id = i };
                row.SetValue("id", i);
                row.SetValue("name", $"User {i}");
                await storage.SaveRowAsync("testdb", "users", row);
            }
            Console.WriteLine("Rows saved");
            
            // Test loading
            var loadedDb = await storage.LoadDatabaseAsync("testdb");
            var loadedTable = await storage.LoadTableAsync("testdb", "users");
            var loadedRows = await storage.LoadRowsAsync("testdb", "users");
            
            Console.WriteLine($"Loaded database: {loadedDb?.Name}");
            Console.WriteLine($"Loaded table: {loadedTable?.Name}");
            Console.WriteLine($"Loaded rows: {loadedRows.Count()}");
            
            // Test backup
            await storage.BackupAsync("./test-backup.json");
            Console.WriteLine("Backup created");
            
            // Test restore
            await storage.RestoreAsync("./test-backup.json");
            Console.WriteLine("Restore completed");
            
            Console.WriteLine("All tests passed!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
        finally
        {
            await storage.CloseAsync();
            storage.Dispose();
            loggerFactory.Dispose();
        }
    }
}
"@

# Write test code to file
$testCode | Out-File -FilePath "FastKvTest.cs" -Encoding UTF8

# Create test project file
$projectFile = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  
  <ItemGroup>
    <ProjectReference Include="src\VnSQL.Core\VnSQL.Core.csproj" />
    <ProjectReference Include="src\VnSQL.Storage\VnSQL.Storage.csproj" />
  </ItemGroup>
  
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Logging" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Logging.Console" Version="8.0.0" />
  </ItemGroup>
</Project>
"@

$projectFile | Out-File -FilePath "FastKvTest.csproj" -Encoding UTF8

# Run the test
Write-Host "Running FastKV test..." -ForegroundColor Yellow
dotnet run --project FastKvTest.csproj

# Cleanup
Write-Host "Cleaning up..." -ForegroundColor Yellow
Remove-Item "FastKvTest.cs" -ErrorAction SilentlyContinue
Remove-Item "FastKvTest.csproj" -ErrorAction SilentlyContinue
Remove-Item "test-fastkv-data" -Recurse -ErrorAction SilentlyContinue
Remove-Item "test-backup.json" -ErrorAction SilentlyContinue

Write-Host "Test completed!" -ForegroundColor Green
