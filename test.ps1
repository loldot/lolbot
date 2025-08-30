$prev = (git rev-parse --short HEAD)

$buildDir = "C:\dev\lolbot\Lolbot.Engine\bin\Release\net9.0\win-x64\publish"
$previousDir = "C:\dev\lolbot-versions\$prev"

$previousVersion = "$previousDir\Lolbot.Engine.exe"
$buildVersion = "$buildDir\Lolbot.Engine.exe"

Push-Location "$(git rev-parse --show-toplevel)\Lolbot.Engine\" 

if (-not (Test-Path $previousDir)) {
    git stash
    git checkout $prev

    mkdir $previousDir

    dotnet publish -c Release -r win-x64
    
    git checkout -
    git stash pop

    Copy-Item "$buildDir\*" $previousDir
}


dotnet publish -c Release -r win-x64

Push-Location "C:\Program Files (x86)\Cute Chess"

./cutechess-cli `
    -engine cmd=$buildVersion name=Experimental `
    -engine cmd=$previousVersion name=Previous `
    -openings file=C:\dev\lolbot-versions\8moves_v3.pgn plies=16 `
    -each proto=uci tc=Inf/10+0.1 `
    -rounds 1000 `
    -games 2 `
    -repeat 2 `
    -recover `
    -sprt elo0=0 elo1=15 alpha=0.05 beta=0.05 #`
    #-debug

Pop-Location
Pop-Location