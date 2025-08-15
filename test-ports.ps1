# Test individual ports
Write-Host "Testing individual ports..." -ForegroundColor Green

$ports = @(3306, 5432, 5433, 1433)

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

Write-Host "`nChecking netstat..." -ForegroundColor Yellow
netstat -an | findstr ":3306\|:5432\|:5433\|:1433"
