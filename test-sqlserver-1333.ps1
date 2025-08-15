# Test SQL Server on port 1333
Write-Host "Testing SQL Server on port 1333..." -ForegroundColor Green

# Test SQL Server connection
try {
    $tcp = New-Object System.Net.Sockets.TcpClient
    $tcp.Connect("localhost", 1333)
    if ($tcp.Connected) {
        Write-Host "OK SQL Server (1333): Connected successfully!" -ForegroundColor Green
        $tcp.Close()
        
        Write-Host "`nTesting with sqlcmd..." -ForegroundColor Yellow
        Write-Host "Command: sqlcmd -S localhost,1333 -U sa -P password" -ForegroundColor White
        Write-Host "Or: sqlcmd -S 127.0.0.1,1333 -U sa -P password" -ForegroundColor White
    }
} catch {
    Write-Host "FAIL SQL Server (1333): $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`nChecking netstat for port 1333..." -ForegroundColor Yellow
netstat -an | findstr ":1333"
