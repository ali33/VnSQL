# Test SQL Server only
Write-Host "Testing SQL Server on port 1333..." -ForegroundColor Green

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
Start-Sleep -Seconds 10

# Test SQL Server connection
try {
    $tcp = New-Object System.Net.Sockets.TcpClient
    $tcp.Connect("localhost", 1333)
    if ($tcp.Connected) {
        Write-Host "OK SQL Server (1333): Connected successfully!" -ForegroundColor Green
        $tcp.Close()
        
        Write-Host "`nTesting with sqlcmd..." -ForegroundColor Yellow
        Write-Host "Command: sqlcmd -S localhost,1333 -U sa -P password" -ForegroundColor White
    }
} catch {
    Write-Host "FAIL SQL Server (1333): $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`nChecking netstat for port 1333..." -ForegroundColor Yellow
netstat -an | findstr ":1333"
