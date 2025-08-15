Write-Host "Testing VnSQL MySQL Server Connection..." -ForegroundColor Green

# Test TCP connection
try {
    $tcpClient = New-Object System.Net.Sockets.TcpClient
    $tcpClient.Connect("localhost", 3306)
    
    if ($tcpClient.Connected) {
        Write-Host "✅ TCP connection successful!" -ForegroundColor Green
        $tcpClient.Close()
    } else {
        Write-Host "❌ TCP connection failed!" -ForegroundColor Red
    }
}
catch {
    Write-Host "❌ Connection error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`nMySQL Connection Commands:" -ForegroundColor Cyan
Write-Host "mysql -h localhost -P 3306 -u root -p" -ForegroundColor Yellow
Write-Host "Password: password" -ForegroundColor Yellow
