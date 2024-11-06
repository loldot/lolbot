$prev = (git rev-parse --short HEAD)

$buildDir = "C:\dev\lolbot\Lolbot.Engine\bin\Release\net8.0\win-x64\publish"
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
    -openings file=C:\dev\lolbot-versions\silver-openings.pgn plies=20 `
    -each proto=uci tc=40/5 `
    -rounds 50 `
    -games 2 `
    -repeat 2 `
    -recover `
    -debug

Pop-Location
Pop-Location