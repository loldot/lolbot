# Test script for UCI Tester
# This script can test individual engines or automatically discover and test all engine versions

param(
    [string]$CommitHash = "",
    [int]$Depth = 8,
    [string[]]$Categories = @("CCC"),
    [switch]$EnableLogging,
    [switch]$Report,
    [int]$ReportLimit = 10,
    [string]$DetailCommit = "",
    [switch]$TestAll,
    [string]$VersionsDir = "C:\dev\lolbot-versions"
)

function Test-IsShortCommitHash {
    param([string]$Name)
    
    # Check if it's a valid short commit hash (7-8 characters, alphanumeric)
    return $Name -match '^[a-f0-9]{7,8}$'
}

function Get-EngineVersions {
    param([string]$BaseDir)
    
    if (!(Test-Path $BaseDir)) {
        Write-Host "Versions directory not found: $BaseDir" -ForegroundColor Red
        return @()
    }
    
    $versions = Get-ChildItem -Path $BaseDir -Directory | Where-Object {
        $enginePath = Join-Path $_.FullName "Lolbot.Engine.exe"
        (Test-IsShortCommitHash $_.Name) -and (Test-Path $enginePath)
    }
    
    return $versions | Sort-Object Name
}

# Build the UCI tester first
Write-Host "Building UCI Tester..." -ForegroundColor Yellow
dotnet build "c:\dev\lolbot\Lolbot.UciTester\Lolbot.UciTester.csproj" --configuration Release

if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to build UCI Tester" -ForegroundColor Red
    exit 1
}

$UciTesterExe = "c:\dev\lolbot\Lolbot.UciTester\bin\Release\net8.0\Lolbot.UciTester.exe"

if (!(Test-Path $UciTesterExe)) {
    Write-Host "UCI Tester not found at: $UciTesterExe" -ForegroundColor Red
    exit 1
}

if ($Report) {
    # Generate report
    Write-Host "Generating performance report..." -ForegroundColor Yellow
    
    if ($DetailCommit -ne "") {
        & $UciTesterExe report --db "test_results.db" --detail $DetailCommit
    } else {
        & $UciTesterExe report --db "test_results.db" --limit $ReportLimit
    }
    
    Write-Host "Report completed!" -ForegroundColor Green
    exit 0
}

if ($TestAll) {
    # Test all engine versions
    Write-Host "Discovering engine versions in: $VersionsDir" -ForegroundColor Yellow
    $versions = Get-EngineVersions -BaseDir $VersionsDir
    
    if ($versions.Count -eq 0) {
        Write-Host "No engine versions found in $VersionsDir" -ForegroundColor Red
        Write-Host "Looking for directories with short commit hash names (7-8 chars) containing Lolbot.Engine.exe" -ForegroundColor Yellow
        exit 1
    }
    
    Write-Host "Found $($versions.Count) engine versions to test:" -ForegroundColor Green
    foreach ($version in $versions) {
        Write-Host "  - $($version.Name)" -ForegroundColor Cyan
    }
    Write-Host ""
    
    $totalVersions = $versions.Count
    $currentVersion = 0
    $successfulTests = 0
    $failedTests = 0
    
    foreach ($version in $versions) {
        $currentVersion++
        $commitHash = $version.Name
        
        Write-Host "[$currentVersion/$totalVersions] Testing engine: $commitHash" -ForegroundColor Yellow
        Write-Host "Engine path: $($version.FullName)\Lolbot.Engine.exe" -ForegroundColor Gray
        
        try {
            $args = @("test", $commitHash, "--depth", $Depth, "--categories") + $Categories + @("--db", "test_results.db", "--engine-dir", $VersionsDir)
            
            if ($EnableLogging) {
                $args += @("--log")
            }
            
            & $UciTesterExe @args
            
            if ($LASTEXITCODE -eq 0) {
                $successfulTests++
                Write-Host "✓ Successfully tested $commitHash" -ForegroundColor Green
            } else {
                $failedTests++
                Write-Host "✗ Failed to test $commitHash (exit code: $LASTEXITCODE)" -ForegroundColor Red
            }
        }
        catch {
            $failedTests++
            Write-Host "✗ Failed to test $commitHash - Exception: $($_.Exception.Message)" -ForegroundColor Red
        }
        
        Write-Host "" # Empty line for readability
    }
    
    Write-Host "=== BATCH TESTING COMPLETED ===" -ForegroundColor Cyan
    Write-Host "Total versions: $totalVersions" -ForegroundColor White
    Write-Host "Successful: $successfulTests" -ForegroundColor Green
    Write-Host "Failed: $failedTests" -ForegroundColor Red
    Write-Host ""
    Write-Host "Run with -Report to see performance comparison" -ForegroundColor Yellow
    exit 0
}

if ($CommitHash -eq "") {
    Write-Host "Error: Either specify a commit hash or use -TestAll to test all versions" -ForegroundColor Red
    Write-Host ""
    Write-Host "Usage examples:" -ForegroundColor Yellow
    Write-Host "  .\test-uci.ps1 abc1234                    # Test specific commit" -ForegroundColor Gray
    Write-Host "  .\test-uci.ps1 -TestAll                   # Test all versions" -ForegroundColor Gray
    Write-Host "  .\test-uci.ps1 -Report                    # Generate report" -ForegroundColor Gray
    Write-Host "  .\test-uci.ps1 -Report -DetailCommit abc1234  # Detailed report" -ForegroundColor Gray
    exit 1
}

# Single commit testing (legacy behavior)
Write-Host "Testing single engine version: $CommitHash" -ForegroundColor Yellow

# Check if testing current build
if ($CommitHash -eq "current") {
    # Build the engine
    Write-Host "Building Lolbot Engine..." -ForegroundColor Yellow
    dotnet build "c:\dev\lolbot\Lolbot.Engine\Lolbot.Engine.csproj" --configuration Release

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Failed to build Lolbot Engine" -ForegroundColor Red
        exit 1
    }

    # Create test engine directory structure
    $EngineBaseDir = "c:\dev\lolbot-versions"
    $CommitDir = Join-Path $EngineBaseDir $CommitHash

    if (!(Test-Path $EngineBaseDir)) {
        New-Item -ItemType Directory -Path $EngineBaseDir -Force | Out-Null
    }

    if (!(Test-Path $CommitDir)) {
        New-Item -ItemType Directory -Path $CommitDir -Force | Out-Null
    }

    # Copy the built engine to the test directory
    $SourceEngine = "c:\dev\lolbot\Lolbot.Engine\bin\Release\net8.0\Lolbot.Engine.exe"
    $TargetEngine = Join-Path $CommitDir "Lolbot.Engine.exe"

    if (Test-Path $SourceEngine) {
        Copy-Item $SourceEngine $TargetEngine -Force
        Write-Host "Copied engine to: $TargetEngine" -ForegroundColor Green
    } else {
        Write-Host "Engine not found at: $SourceEngine" -ForegroundColor Red
        Write-Host "Make sure the engine builds successfully in Release mode" -ForegroundColor Red
        exit 1
    }
    
    $engineDir = "c:\dev\lolbot-versions"
} else {
    # Use the versions directory for existing commits
    $engineDir = $VersionsDir
}

# Run the UCI tester
Write-Host "Running UCI Tester..." -ForegroundColor Yellow

$args = @("test", $CommitHash, "--depth", $Depth, "--categories") + $Categories + @("--db", "test_results.db", "--engine-dir", $engineDir)

if ($EnableLogging) {
    $args += @("--log")
    Write-Host "UCI communication logging enabled" -ForegroundColor Green
}

& $UciTesterExe @args

if ($LASTEXITCODE -eq 0) {
    Write-Host "Test completed successfully!" -ForegroundColor Green
    Write-Host "Results saved to test_results.db" -ForegroundColor Green
} else {
    Write-Host "Test failed with exit code: $LASTEXITCODE" -ForegroundColor Red
    exit 1
}