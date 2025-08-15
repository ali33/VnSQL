# Test SQL Server Connection Script for VnSQL
Write-Host "Testing VnSQL SQL Server Connection..." -ForegroundColor Green

# Stop existing VnSQL processes
Write-Host "Stopping existing VnSQL processes..." -ForegroundColor Yellow
Get-Process -Name "VnSQL.Server" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2

# Build the project
Write-Host "Building VnSQL project..." -ForegroundColor Yellow
dotnet build
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# Start the server
Write-Host "Starting VnSQL server..." -ForegroundColor Yellow
Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", "src/VnSQL.Server" -WindowStyle Hidden
$serverProcess = Get-Process -Name "dotnet" | Where-Object { $_.CommandLine -like "*VnSQL.Server*" } | Select-Object -First 1

# Wait for server to start
Write-Host "Waiting for server to start..." -ForegroundColor Yellow
Start-Sleep -Seconds 5

# Test TCP connection to SQL Server port
Write-Host "Testing TCP connection to SQL Server port 1433..." -ForegroundColor Yellow
try {
    $tcpClient = New-Object System.Net.Sockets.TcpClient
    $tcpClient.Connect("localhost", 1433)
    if ($tcpClient.Connected) {
        Write-Host "✓ TCP connection to SQL Server port 1433 successful!" -ForegroundColor Green
        $tcpClient.Close()
    } else {
        Write-Host "✗ TCP connection to SQL Server port 1433 failed!" -ForegroundColor Red
    }
} catch {
    Write-Host "✗ TCP connection to SQL Server port 1433 failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`nSQL Server Protocol Handler Test Complete!" -ForegroundColor Green
Write-Host "Server is running in background. To stop it, run: Get-Process -Name 'dotnet' | Where-Object { `$_.CommandLine -like '*VnSQL.Server*' } | Stop-Process" -ForegroundColor Yellow

Write-Host "`nTo test with SQL Server client tools:" -ForegroundColor Cyan
Write-Host "1. Use sqlcmd: sqlcmd -S localhost,1433 -U sa -P password" -ForegroundColor White
Write-Host "2. Use SQL Server Management Studio (SSMS)" -ForegroundColor White
Write-Host "3. Use Azure Data Studio" -ForegroundColor White
Write-Host "4. Use any TDS-compatible client" -ForegroundColor White

Write-Host "`nSupported SQL Server commands:" -ForegroundColor Cyan
Write-Host "- SELECT * FROM table;" -ForegroundColor White
Write-Host "- CREATE TABLE table (column1 INT, column2 VARCHAR(255));" -ForegroundColor White
Write-Host "- INSERT INTO table VALUES (1, 'test');" -ForegroundColor White
Write-Host "- UPDATE table SET column1 = 2 WHERE column1 = 1;" -ForegroundColor White
Write-Host "- DELETE FROM table WHERE column1 = 2;" -ForegroundColor White
Write-Host "- DROP TABLE table;" -ForegroundColor White
