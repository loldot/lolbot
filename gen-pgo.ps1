
$Env:DOTNET_EnableEventPipe=1
$Env:DOTNET_ReadyToRun=0
$Env:DOTNET_TieredPGO=1
$Env:DOTNET_TieredPGO_InstrumentOnlyHotCode=0
$Env:DOTNET_TC_CallCounting=0
$Env:DOTNET_TC_QuickJitForLoops=1
$Env:DOTNET_JitCollect64BitCounts=1

dotnet trace collect -p 68280 --providers Microsoft-Windows-DotNETRuntime:0x6000080018:5 --format nettrace --output pgo.nettrace