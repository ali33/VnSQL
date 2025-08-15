# Test with logs
Write-Host "Testing VnSQL with logs..." -ForegroundColor Green

# Stop existing processes
Get-Process -Name "VnSQL.Server" -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Where-Object { $_.CommandLine -like "*VnSQL.Server*" } | Stop-Process -Force
Start-Sleep -Seconds 2

# Build
dotnet build

# Start server and capture output
Write-Host "Starting server..." -ForegroundColor Yellow
$process = Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", "src/VnSQL.Server" -WindowStyle Hidden -PassThru

# Wait for server to start
Start-Sleep -Seconds 10

Write-Host "Testing ports..." -ForegroundColor Yellow

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

Write-Host "`nServer process ID: $($process.Id)" -ForegroundColor Cyan
Write-Host "To stop server: Stop-Process -Id $($process.Id) -Force" -ForegroundColor Yellow
