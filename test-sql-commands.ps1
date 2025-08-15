Write-Host "Testing VnSQL SQL Commands..." -ForegroundColor Green

# Stop any existing VnSQL processes
try {
    Get-Process | Where-Object {$_.ProcessName -like "*VnSQL*"} | Stop-Process -Force
    Start-Sleep -Seconds 2
    Write-Host "✅ Stopped existing VnSQL processes" -ForegroundColor Green
}
catch {
    Write-Host "ℹ️  No existing VnSQL processes found" -ForegroundColor Yellow
}

# Start VnSQL server
Write-Host "Starting VnSQL server..." -ForegroundColor Yellow
$serverProcess = Start-Process -FilePath "dotnet" -ArgumentList "run --project src/VnSQL.Server" -PassThru -WindowStyle Hidden

# Wait for server to start
Start-Sleep -Seconds 5

Write-Host "`nVnSQL Server is running!" -ForegroundColor Green
Write-Host "You can now test SQL commands with MySQL client:" -ForegroundColor Cyan
Write-Host "mysql -h localhost -P 3306 -u root -p" -ForegroundColor White
Write-Host "Password: password" -ForegroundColor White

Write-Host "`nSupported SQL Commands:" -ForegroundColor Yellow
Write-Host "1. SHOW DATABASES;" -ForegroundColor White
Write-Host "2. CREATE DATABASE testdb;" -ForegroundColor White
Write-Host "3. USE testdb;" -ForegroundColor White
Write-Host "4. CREATE TABLE users (id INT PRIMARY KEY, name VARCHAR(255), email VARCHAR(255));" -ForegroundColor White
Write-Host "5. SHOW TABLES;" -ForegroundColor White
Write-Host "6. DESCRIBE users;" -ForegroundColor White
Write-Host "7. INSERT INTO users (id, name, email) VALUES (1, 'John Doe', 'john@example.com');" -ForegroundColor White
Write-Host "8. SELECT * FROM users;" -ForegroundColor White
Write-Host "9. UPDATE users SET name = 'Jane Doe' WHERE id = 1;" -ForegroundColor White
Write-Host "10. DELETE FROM users WHERE id = 1;" -ForegroundColor White
Write-Host "11. DROP TABLE users;" -ForegroundColor White
Write-Host "12. DROP DATABASE testdb;" -ForegroundColor White

Write-Host "`nPostgreSQL-style commands also supported:" -ForegroundColor Yellow
Write-Host "1. SELECT datname FROM pg_database;" -ForegroundColor White
Write-Host "2. SELECT tablename FROM pg_tables WHERE schemaname = 'public';" -ForegroundColor White
Write-Host "3. SELECT column_name, data_type, is_nullable FROM information_schema.columns WHERE table_name = 'users';" -ForegroundColor White

Write-Host "`nServer is running in background. Press Ctrl+C to stop." -ForegroundColor Yellow

# Keep server running
try {
    Wait-Process -Id $serverProcess.Id
}
catch {
    Write-Host "Server stopped." -ForegroundColor Yellow
}
