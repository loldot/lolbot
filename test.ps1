$prev =(git rev-parse --short HEAD)

Push-Location "$(git rev-parse --show-toplevel)\Lolbot.Engine\" 
dotnet publish -c Release -r win-x64

Push-Location "C:\Program Files (x86)\Cute Chess"

./cutechess-cli `
    -engine cmd=C:\dev\lolbot\Lolbot.Engine\bin\Release\net8.0\win-x64\publish\Lolbot.Engine.exe name=Experimental `
    -engine cmd=C:\dev\lolbot-versions\$prev\Lolbot.Engine.exe name=Previous `
    -openings file=C:\dev\lolbot-versions\silver-openings.pgn plies=20 `
    -each proto=uci tc=40/5 `
    -rounds 50 `
    -games 2

Pop-Location
Pop-Location