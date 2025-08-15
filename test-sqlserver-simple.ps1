# Simple SQL Server Test
Write-Host "Simple SQL Server Test" -ForegroundColor Green

# Stop existing processes
Get-Process -Name "VnSQL.Server" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2

# Build and start server
dotnet build
Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", "src/VnSQL.Server" -WindowStyle Hidden

# Wait for server to start
Start-Sleep -Seconds 10

# Test TCP connection
try {
    $tcp = New-Object System.Net.Sockets.TcpClient
    $tcp.Connect("localhost", 1333)
    if ($tcp.Connected) {
        Write-Host "OK TCP connection to port 1333 successful!" -ForegroundColor Green
        $tcp.Close()
        
        Write-Host "`nNow test with sqlcmd:" -ForegroundColor Yellow
        Write-Host "sqlcmd -S localhost,1333 -U sa -P password" -ForegroundColor White
    }
} catch {
    Write-Host "FAIL TCP connection failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`nServer is running in background. Press any key to stop..." -ForegroundColor Cyan
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
