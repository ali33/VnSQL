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

Write-Host "`n=== Test Commands ===" -ForegroundColor Magenta
Write-Host "SHOW DATABASES;" -ForegroundColor White
Write-Host "CREATE DATABASE testdb;" -ForegroundColor White
Write-Host "USE testdb;" -ForegroundColor White
Write-Host "CREATE TABLE users (id INT PRIMARY KEY, name VARCHAR(50), email VARCHAR(100));" -ForegroundColor White
Write-Host "INSERT INTO users (id, name, email) VALUES (1, 'John Doe', 'john@example.com');" -ForegroundColor White
Write-Host "SELECT * FROM users;" -ForegroundColor White
Write-Host "UPDATE users SET email = 'john.updated@example.com' WHERE id = 1;" -ForegroundColor White
Write-Host "DELETE FROM users WHERE id = 1;" -ForegroundColor White

Write-Host "`n=== Manual Testing Instructions ===" -ForegroundColor Magenta
Write-Host "1. Open MySQL client: mysql -h localhost -P 3306 -u root -p" -ForegroundColor White
Write-Host "2. Enter password: password" -ForegroundColor White
Write-Host "3. Run the test commands above one by one" -ForegroundColor White

Write-Host "`nServer is running. Press Ctrl+C to stop." -ForegroundColor Yellow
