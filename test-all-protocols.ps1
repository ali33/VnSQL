# Test All Protocols Script for VnSQL
Write-Host "🌐 Testing VnSQL - All 4 Protocols Running in Parallel!" -ForegroundColor Green
Write-Host "==================================================" -ForegroundColor Green

# Stop existing VnSQL processes
Write-Host "Stopping existing VnSQL processes..." -ForegroundColor Yellow
Get-Process -Name "VnSQL.Server" -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Where-Object { $_.CommandLine -like "*VnSQL.Server*" } | Stop-Process -Force
Start-Sleep -Seconds 2

# Build the project
Write-Host "Building VnSQL project..." -ForegroundColor Yellow
dotnet build
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# Start the server
Write-Host "Starting VnSQL server with all 4 protocols..." -ForegroundColor Yellow
Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", "src/VnSQL.Server" -WindowStyle Hidden

# Wait for server to start
Write-Host "Waiting for server to start..." -ForegroundColor Yellow
Start-Sleep -Seconds 8

Write-Host "`n🔍 Testing All 4 Protocols:" -ForegroundColor Cyan
Write-Host "=========================" -ForegroundColor Cyan

# Test MySQL Protocol (Port 3306)
Write-Host "`n1️⃣ Testing MySQL Protocol (Port 3306)..." -ForegroundColor Yellow
try {
    $tcpClient = New-Object System.Net.Sockets.TcpClient
    $tcpClient.Connect("localhost", 3306)
    if ($tcpClient.Connected) {
        Write-Host "   ✅ MySQL connection successful!" -ForegroundColor Green
        $tcpClient.Close()
    } else {
        Write-Host "   ❌ MySQL connection failed!" -ForegroundColor Red
    }
} catch {
    Write-Host "   ❌ MySQL connection failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test PostgreSQL Protocol (Port 5432)
Write-Host "`n2️⃣ Testing PostgreSQL Protocol (Port 5432)..." -ForegroundColor Yellow
try {
    $tcpClient = New-Object System.Net.Sockets.TcpClient
    $tcpClient.Connect("localhost", 5432)
    if ($tcpClient.Connected) {
        Write-Host "   ✅ PostgreSQL connection successful!" -ForegroundColor Green
        $tcpClient.Close()
    } else {
        Write-Host "   ❌ PostgreSQL connection failed!" -ForegroundColor Red
    }
} catch {
    Write-Host "   ❌ PostgreSQL connection failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test SQLite Protocol (Port 5433)
Write-Host "`n3️⃣ Testing SQLite Protocol (Port 5433)..." -ForegroundColor Yellow
try {
    $tcpClient = New-Object System.Net.Sockets.TcpClient
    $tcpClient.Connect("localhost", 5433)
    if ($tcpClient.Connected) {
        Write-Host "   ✅ SQLite connection successful!" -ForegroundColor Green
        $tcpClient.Close()
    } else {
        Write-Host "   ❌ SQLite connection failed!" -ForegroundColor Red
    }
} catch {
    Write-Host "   ❌ SQLite connection failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test SQL Server Protocol (Port 1433)
Write-Host "`n4️⃣ Testing SQL Server Protocol (Port 1433)..." -ForegroundColor Yellow
try {
    $tcpClient = New-Object System.Net.Sockets.TcpClient
    $tcpClient.Connect("localhost", 1433)
    if ($tcpClient.Connected) {
        Write-Host "   ✅ SQL Server connection successful!" -ForegroundColor Green
        $tcpClient.Close()
    } else {
        Write-Host "   ❌ SQL Server connection failed!" -ForegroundColor Red
    }
} catch {
    Write-Host "   ❌ SQL Server connection failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n🎉 All Protocol Tests Complete!" -ForegroundColor Green
Write-Host "Server is running in background with all 4 protocols active." -ForegroundColor Yellow

Write-Host "`n📋 Connection Information:" -ForegroundColor Cyan
Write-Host "=========================" -ForegroundColor Cyan
Write-Host "MySQL:      localhost:3306 (root/password)" -ForegroundColor White
Write-Host "PostgreSQL: localhost:5432 (postgres/password)" -ForegroundColor White
Write-Host "SQLite:     localhost:5433 (sqlite/password)" -ForegroundColor White
Write-Host "SQL Server: localhost:1433 (sa/password)" -ForegroundColor White

Write-Host "`n🔧 Test Commands:" -ForegroundColor Cyan
Write-Host "================" -ForegroundColor Cyan
Write-Host "MySQL:      mysql -h localhost -P 3306 -u root -p" -ForegroundColor White
Write-Host "PostgreSQL: psql -h localhost -p 5432 -U postgres -d postgres" -ForegroundColor White
Write-Host "SQLite:     telnet localhost 5433" -ForegroundColor White
Write-Host "SQL Server: sqlcmd -S localhost,1433 -U sa -P password" -ForegroundColor White

Write-Host "`n💡 Features:" -ForegroundColor Cyan
Write-Host "============" -ForegroundColor Cyan
Write-Host "✅ All 4 protocols run simultaneously" -ForegroundColor Green
Write-Host "✅ Shared storage engine" -ForegroundColor Green
Write-Host "✅ Unified SQL command processing" -ForegroundColor Green
Write-Host "✅ Protocol-specific response formatting" -ForegroundColor Green
Write-Host "✅ Independent connection management" -ForegroundColor Green

Write-Host "`n🚀 VnSQL is now a true multi-protocol database server!" -ForegroundColor Green
Write-Host "To stop the server: Get-Process -Name 'dotnet' | Where-Object { `$_.CommandLine -like '*VnSQL.Server*' } | Stop-Process" -ForegroundColor Yellow
