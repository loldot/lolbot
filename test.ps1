# Optional parameter for previous commit hash:

Param(
    $prev = (git rev-parse --short HEAD~1)
)

Write-Host "Comparing against previous version: ($prev)"
$sourceDir =  Join-Path -path $(git rev-parse --show-toplevel) -ChildPath "Lolbot.Engine"
$buildDir = Join-Path -path $sourceDir -ChildPath "bin\Release\net10.0\win-x64\publish\"
$previousDir = "C:\dev\lolbot-versions\$prev"

Write-Host("Source dir: $sourceDir")
Write-Host("Build dir: $buildDir")
Write-Host("Previous dir: $previousDir")


$previousVersion = "$previousDir\Lolbot.Engine.exe"
$buildVersion = "$buildDir\Lolbot.Engine.exe"

Push-Location "$(git rev-parse --show-toplevel)\Lolbot.Engine\" 

$stashed = $false
if (-not (Test-Path $previousDir)) {
    if (-not (git diff --quiet --exit-code)) {
        git stash
        $stashed = $true
    }
    
    git checkout $prev

    mkdir $previousDir

    dotnet publish -c Release -r win-x64
    
    git checkout -

    if ($stashed) {
        $stashed = $false
        git stash pop
    }
    

    Copy-Item "$buildDir\*" $previousDir
}

dotnet publish -c Release -r win-x64

Push-Location "C:\Program Files (x86)\Cute Chess"

Write-Host "Tournament:"
Write-Host "Experimental: $buildVersion"
Write-Host "Previous: $previousVersion"

./cutechess-cli `
    -engine cmd=$buildVersion name=Experimental `
    -engine cmd=$previousVersion name=Previous `
    -openings file=C:\dev\lolbot-versions\8moves_v3.pgn plies=16 `
    -each proto=uci tc=Inf/10+0.1 `
    -rounds 1000 `
    -games 2 `
    -repeat 2 `
    -recover `
    -sprt elo0=0 elo1=15 alpha=0.05 beta=0.05 `
    -tb "C:\dev\chess-data\syzygy\3-4-5-wdl" `
    -pgnout "C:\dev\chess-data\$(Get-Date -Format yyyyMMddHHmmss).pgn" fi `
    -concurrency 4 `
| Tee-Object C:\temp\log.txt

Pop-Location
Pop-Location