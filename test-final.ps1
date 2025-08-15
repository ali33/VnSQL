# Final Test - All Protocols
Write-Host "Final Test - All 4 Protocols" -ForegroundColor Green

# Stop existing processes
Get-Process -Name "VnSQL.Server" -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Where-Object { $_.CommandLine -like "*VnSQL.Server*" } | Stop-Process -Force
Start-Sleep -Seconds 2

# Build
dotnet build

# Start server
Write-Host "Starting server..." -ForegroundColor Yellow
Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", "src/VnSQL.Server" -WindowStyle Hidden

# Wait for server to start
Start-Sleep -Seconds 15

Write-Host "Testing all protocols..." -ForegroundColor Yellow

# Test each port
$ports = @(3306, 5432, 5433, 1333)
foreach ($port in $ports) {
    try {
        $tcp = New-Object System.Net.Sockets.TcpClient
        $tcp.Connect("localhost", $port)
        if ($tcp.Connected) {
            Write-Host "OK Port $port OK" -ForegroundColor Green
            $tcp.Close()
        }
    } catch {
        Write-Host "FAIL Port $port Failed - $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "`nConnection info:" -ForegroundColor Cyan
Write-Host "MySQL: localhost:3306 (root/password)" -ForegroundColor White
Write-Host "PostgreSQL: localhost:5432 (postgres/password)" -ForegroundColor White
Write-Host "SQLite: localhost:5433 (sqlite/password)" -ForegroundColor White
Write-Host "SQL Server: localhost:1333 (sa/password)" -ForegroundColor White

Write-Host "`nTest commands:" -ForegroundColor Cyan
Write-Host "MySQL: mysql -h localhost -P 3306 -u root -p" -ForegroundColor White
Write-Host "PostgreSQL: psql -h localhost -p 5432 -U postgres -d postgres" -ForegroundColor White
Write-Host "SQLite: telnet localhost 5433" -ForegroundColor White
Write-Host "SQL Server: sqlcmd -S localhost,1333 -U sa -P password" -ForegroundColor White
