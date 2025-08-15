Write-Host "Testing VnSQL MySQL Connection Stability..." -ForegroundColor Green

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

# Test multiple connections
Write-Host "Testing multiple MySQL connections..." -ForegroundColor Yellow

for ($i = 1; $i -le 3; $i++) {
    Write-Host "Connection test $i/3..." -ForegroundColor Cyan
    
    try {
        # Test TCP connection
        $tcpClient = New-Object System.Net.Sockets.TcpClient
        $tcpClient.Connect("localhost", 3306)
        
        if ($tcpClient.Connected) {
            Write-Host "✅ TCP connection $i successful" -ForegroundColor Green
            $tcpClient.Close()
        }
        else {
            Write-Host "❌ TCP connection $i failed" -ForegroundColor Red
        }
    }
    catch {
        Write-Host "❌ TCP connection $i failed: $($_.Exception.Message)" -ForegroundColor Red
    }
    
    Start-Sleep -Seconds 2
}

Write-Host "`nConnection stability test completed!" -ForegroundColor Green
Write-Host "Server is running in background. Press Ctrl+C to stop." -ForegroundColor Yellow
Write-Host "`nTo test with MySQL client:" -ForegroundColor Cyan
Write-Host "mysql -h localhost -P 3306 -u root -p" -ForegroundColor White
Write-Host "Password: password" -ForegroundColor White

# Keep server running
try {
    Wait-Process -Id $serverProcess.Id
}
catch {
    Write-Host "Server stopped." -ForegroundColor Yellow
}
