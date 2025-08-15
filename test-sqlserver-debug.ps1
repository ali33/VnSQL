# Debug SQL Server Connection
Write-Host "Debug SQL Server Connection" -ForegroundColor Green

# Stop existing processes
Get-Process -Name "VnSQL.Server" -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Where-Object { $_.CommandLine -like "*VnSQL.Server*" } | Stop-Process -Force
Start-Sleep -Seconds 2

# Build
dotnet build

# Start server in foreground to see logs
Write-Host "Starting server in foreground..." -ForegroundColor Yellow
Write-Host "Press Ctrl+C to stop after testing" -ForegroundColor Red

# Start server in foreground
dotnet run --project src/VnSQL.Server
