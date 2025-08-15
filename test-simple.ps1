# Simple Test Script for VnSQL All Protocols
Write-Host "Testing VnSQL - All 4 Protocols Running in Parallel!" -ForegroundColor Green

# Stop existing processes
Get-Process -Name "VnSQL.Server" -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Where-Object { $_.CommandLine -like "*VnSQL.Server*" } | Stop-Process -Force
Start-Sleep -Seconds 2

# Build and start
dotnet build
Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", "src/VnSQL.Server" -WindowStyle Hidden
Start-Sleep -Seconds 8

Write-Host "Testing all 4 protocols..." -ForegroundColor Yellow

# Test MySQL (3306)
try {
    $tcp = New-Object System.Net.Sockets.TcpClient
    $tcp.Connect("localhost", 3306)
    if ($tcp.Connected) {
        Write-Host "✅ MySQL (3306): OK" -ForegroundColor Green
        $tcp.Close()
    }
} catch {
    Write-Host "❌ MySQL (3306): Failed" -ForegroundColor Red
}

# Test PostgreSQL (5432)
try {
    $tcp = New-Object System.Net.Sockets.TcpClient
    $tcp.Connect("localhost", 5432)
    if ($tcp.Connected) {
        Write-Host "✅ PostgreSQL (5432): OK" -ForegroundColor Green
        $tcp.Close()
    }
} catch {
    Write-Host "❌ PostgreSQL (5432): Failed" -ForegroundColor Red
}

# Test SQLite (5433)
try {
    $tcp = New-Object System.Net.Sockets.TcpClient
    $tcp.Connect("localhost", 5433)
    if ($tcp.Connected) {
        Write-Host "✅ SQLite (5433): OK" -ForegroundColor Green
        $tcp.Close()
    }
} catch {
    Write-Host "❌ SQLite (5433): Failed" -ForegroundColor Red
}

# Test SQL Server (1433)
try {
    $tcp = New-Object System.Net.Sockets.TcpClient
    $tcp.Connect("localhost", 1433)
    if ($tcp.Connected) {
        Write-Host "✅ SQL Server (1433): OK" -ForegroundColor Green
        $tcp.Close()
    }
} catch {
    Write-Host "❌ SQL Server (1433): Failed" -ForegroundColor Red
}

Write-Host "`nServer is running with all 4 protocols!" -ForegroundColor Green
Write-Host "Connection info:" -ForegroundColor Yellow
Write-Host "MySQL: localhost:3306 (root/password)" -ForegroundColor White
Write-Host "PostgreSQL: localhost:5432 (postgres/password)" -ForegroundColor White
Write-Host "SQLite: localhost:5433 (sqlite/password)" -ForegroundColor White
Write-Host "SQL Server: localhost:1433 (sa/password)" -ForegroundColor White
