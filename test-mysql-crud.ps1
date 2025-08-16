# Test MySQL CRUD Operations
Write-Host "=== Testing MySQL CRUD Operations ===" -ForegroundColor Green

# Stop any existing server
Write-Host "Stopping existing server..." -ForegroundColor Yellow
Get-Process -Name "VnSQL.Server" -ErrorAction SilentlyContinue | Stop-Process -Force

# Start server
Write-Host "Starting VnSQL server..." -ForegroundColor Yellow
Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", "src/VnSQL.Server" -WindowStyle Hidden

# Wait for server to start
Write-Host "Waiting for server to start..." -ForegroundColor Yellow
Start-Sleep -Seconds 3

# Test MySQL connection and CRUD operations
Write-Host "Testing MySQL CRUD operations..." -ForegroundColor Cyan

# Test commands to run
$testCommands = @(
    "SHOW DATABASES;",
    "CREATE DATABASE testdb;",
    "USE testdb;",
    "SHOW TABLES;",
    "CREATE TABLE users (id INT PRIMARY KEY, name VARCHAR(50), email VARCHAR(100));",
    "DESCRIBE users;",
    "INSERT INTO users (id, name, email) VALUES (1, 'John Doe', 'john@example.com');",
    "INSERT INTO users (id, name, email) VALUES (2, 'Jane Smith', 'jane@example.com');",
    "SELECT * FROM users;",
    "SELECT name, email FROM users WHERE id = 1;",
    "UPDATE users SET email = 'john.updated@example.com' WHERE id = 1;",
    "SELECT * FROM users;",
    "DELETE FROM users WHERE id = 2;",
    "SELECT * FROM users;",
    "DROP TABLE users;",
    "SHOW TABLES;",
    "DROP DATABASE testdb;",
    "SHOW DATABASES;"
)

Write-Host "`n=== Test Commands ===" -ForegroundColor Magenta
foreach ($cmd in $testCommands) {
    Write-Host "Command: $cmd" -ForegroundColor White
}

Write-Host "`n=== Manual Testing Instructions ===" -ForegroundColor Magenta
Write-Host "1. Open MySQL client: mysql -h localhost -P 3306 -u root -p" -ForegroundColor White
Write-Host "2. Enter password: password" -ForegroundColor White
Write-Host "3. Run the test commands above one by one" -ForegroundColor White
Write-Host "4. Check that each command executes successfully" -ForegroundColor White

Write-Host "`n=== Expected Results ===" -ForegroundColor Magenta
Write-Host "✓ SHOW DATABASES: Should show available databases" -ForegroundColor Green
Write-Host "✓ CREATE DATABASE: Should create testdb successfully" -ForegroundColor Green
Write-Host "✓ USE DATABASE: Should switch to testdb" -ForegroundColor Green
Write-Host "✓ CREATE TABLE: Should create users table with 3 columns" -ForegroundColor Green
Write-Host "✓ DESCRIBE TABLE: Should show table structure" -ForegroundColor Green
Write-Host "✓ INSERT: Should insert 2 rows successfully" -ForegroundColor Green
Write-Host "✓ SELECT: Should return inserted data" -ForegroundColor Green
Write-Host "✓ UPDATE: Should update email for id=1" -ForegroundColor Green
Write-Host "✓ DELETE: Should delete row with id=2" -ForegroundColor Green
Write-Host "✓ DROP TABLE: Should remove users table" -ForegroundColor Green
Write-Host "✓ DROP DATABASE: Should remove testdb" -ForegroundColor Green

Write-Host "`nServer is running. Press Ctrl+C to stop." -ForegroundColor Yellow
Write-Host "Or run: Get-Process -Name VnSQL.Server | Stop-Process" -ForegroundColor Gray
