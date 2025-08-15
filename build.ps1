# VnSQL Build Script
# PowerShell script để build và chạy dự án VnSQL

param(
    [switch]$Clean,
    [switch]$Test,
    [switch]$Run,
    [switch]$Help
)

if ($Help) {
    Write-Host "VnSQL Build Script" -ForegroundColor Green
    Write-Host "==================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Usage:" -ForegroundColor Yellow
    Write-Host "  .\build.ps1 [-Clean] [-Test] [-Run] [-Help]" -ForegroundColor White
    Write-Host ""
    Write-Host "Options:" -ForegroundColor Yellow
    Write-Host "  -Clean    Clean build outputs" -ForegroundColor White
    Write-Host "  -Test     Run tests" -ForegroundColor White
    Write-Host "  -Run      Build and run the server" -ForegroundColor White
    Write-Host "  -Help     Show this help message" -ForegroundColor White
    Write-Host ""
    Write-Host "Examples:" -ForegroundColor Yellow
    Write-Host "  .\build.ps1 -Clean -Test" -ForegroundColor White
    Write-Host "  .\build.ps1 -Run" -ForegroundColor White
    exit 0
}

Write-Host "🌐 VnSQL - SQL Server Phân tán cho người Việt Nam 🇻🇳" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host ""

# Check if .NET 8.0 is installed
try {
    $dotnetVersion = dotnet --version
    Write-Host "✅ .NET version: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "❌ .NET 8.0 is not installed. Please install .NET 8.0 SDK." -ForegroundColor Red
    exit 1
}

# Clean if requested
if ($Clean) {
    Write-Host "🧹 Cleaning build outputs..." -ForegroundColor Yellow
    dotnet clean
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Clean failed" -ForegroundColor Red
        exit 1
    }
    Write-Host "✅ Clean completed" -ForegroundColor Green
}

# Restore packages
Write-Host "📦 Restoring packages..." -ForegroundColor Yellow
dotnet restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Package restore failed" -ForegroundColor Red
    exit 1
}
Write-Host "✅ Packages restored" -ForegroundColor Green

# Build solution
Write-Host "🔨 Building solution..." -ForegroundColor Yellow
dotnet build --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed" -ForegroundColor Red
    exit 1
}
Write-Host "✅ Build completed" -ForegroundColor Green

# Run tests if requested
if ($Test) {
    Write-Host "🧪 Running tests..." -ForegroundColor Yellow
    dotnet test --no-build --verbosity normal
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Tests failed" -ForegroundColor Red
        exit 1
    }
    Write-Host "✅ Tests passed" -ForegroundColor Green
}

# Run server if requested
if ($Run) {
    Write-Host "🚀 Starting VnSQL Server..." -ForegroundColor Yellow
    Write-Host "Press Ctrl+C to stop the server" -ForegroundColor Gray
    Write-Host ""
    
    # Change to server directory and run
    Push-Location "src\VnSQL.Server"
    try {
        dotnet run --no-build
    } finally {
        Pop-Location
    }
}

Write-Host ""
Write-Host "🎉 Build script completed successfully!" -ForegroundColor Green
