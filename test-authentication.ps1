Write-Host "Testing MySQL Authentication..." -ForegroundColor Green

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

Write-Host "`nMySQL Authentication Test:" -ForegroundColor Cyan
Write-Host "Server is running with authentication enabled." -ForegroundColor Yellow
Write-Host "Try connecting with these credentials:" -ForegroundColor Yellow
Write-Host "Username: root" -ForegroundColor White
Write-Host "Password: password" -ForegroundColor White
Write-Host "`nCommand: mysql -h localhost -P 3306 -u root -p" -ForegroundColor Green

Write-Host "`nExpected behavior:" -ForegroundColor Magenta
Write-Host "- ✅ Correct credentials (root/password): Authentication successful" -ForegroundColor Green
Write-Host "- ❌ Wrong credentials: Access denied" -ForegroundColor Red
Write-Host "- 📝 Check server logs for detailed authentication process" -ForegroundColor Yellow

Write-Host "`nPress Ctrl+C to stop the server..." -ForegroundColor Magenta

# Keep server running until user stops it
try {
    while ($true) {
        Start-Sleep -Seconds 1
        if ($serverProcess.HasExited) {
            Write-Host "Server stopped unexpectedly" -ForegroundColor Red
            break
        }
    }
}
catch {
    Write-Host "`nStopping server..." -ForegroundColor Yellow
    try {
        Stop-Process -Id $serverProcess.Id -Force
        Write-Host "✅ Server stopped" -ForegroundColor Green
    }
    catch {
        Write-Host "ℹ️  Server already stopped" -ForegroundColor Yellow
    }
}
