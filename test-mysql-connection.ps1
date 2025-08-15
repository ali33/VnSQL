Write-Host "Testing MySQL Connection to VnSQL Server..." -ForegroundColor Green

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

Write-Host "`nMySQL Connection Commands:" -ForegroundColor Cyan
Write-Host "mysql -h localhost -P 3306 -u root -p" -ForegroundColor Yellow
Write-Host "Password: password (or any password)" -ForegroundColor Yellow

Write-Host "`nServer is running. Try connecting with MySQL client." -ForegroundColor Magenta
Write-Host "Press Ctrl+C to stop the server..." -ForegroundColor Magenta

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
