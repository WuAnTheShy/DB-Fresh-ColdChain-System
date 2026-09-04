param([int]$ShopPort = 5064, [int]$CarrierPort = 5077, [switch]$Seed)
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $repoPath
if ($ShopPort -eq $CarrierPort -or $ShopPort -lt 1024 -or $CarrierPort -lt 1024 -or $ShopPort -gt 65535 -or $CarrierPort -gt 65535) { throw '请指定两个不同的有效端口（1024–65535）' }
foreach ($port in @($ShopPort, $CarrierPort)) {
    if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) { throw "端口 $port 已被占用，请先关闭原服务或改用其他端口" }
}
dotnet build FreshColdChain.csproj -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw '商城编译失败' }
dotnet build CarrierSimulator/CarrierSimulator.csproj -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw '模拟器编译失败' }
if ($Seed) {
    dotnet run --project tests/CarrierDemoFixture -c Release -- --seed
    if ($LASTEXITCODE -ne 0) { throw '演示数据创建失败，未启动服务' }
}
$demoKey = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$logPath = Join-Path $repoPath 'tmp/carrier-demo'
$null = New-Item -ItemType Directory -Path $logPath -Force
$logHandles = [Collections.Generic.List[object]]::new()
function Start-DemoService([string]$dll, [string]$workingDirectory, [hashtable]$settings) {
    $info = [Diagnostics.ProcessStartInfo]::new('dotnet')
    $info.ArgumentList.Add($dll)
    $info.WorkingDirectory = $workingDirectory
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.Environment['ASPNETCORE_ENVIRONMENT'] = 'Development'
    foreach ($entry in $settings.GetEnumerator()) { $info.Environment[$entry.Key] = [string]$entry.Value }
    $process = [Diagnostics.Process]::Start($info)
    $name = [IO.Path]::GetFileNameWithoutExtension($dll)
    foreach ($kind in @('out', 'err')) {
        $stream = [IO.File]::Create((Join-Path $logPath "$name-$($process.Id).$kind.log"))
        $reader = if ($kind -eq 'out') { $process.StandardOutput } else { $process.StandardError }
        $logHandles.Add(@{ Stream=$stream; Copy=$reader.BaseStream.CopyToAsync($stream) })
    }
    return $process
}
$shop = $null
$carrier = $null
try {
    $shop = Start-DemoService "$repoPath/bin/Release/net10.0/FreshColdChain.dll" $repoPath @{
        ASPNETCORE_URLS = "http://localhost:$ShopPort"
        GroupA__Logistics__Provider = 'Oracle'
        GroupA__DemoCarrier__Enabled = 'true'
        GroupA__DemoCarrier__ApiKey = $demoKey
        GroupA__DemoCarrier__SupplierIds__0 = 'SUP-CARRIER-DEMO'
    }
    $carrier = Start-DemoService "$repoPath/CarrierSimulator/bin/Release/net10.0/CarrierSimulator.dll" "$repoPath/CarrierSimulator" @{
        ASPNETCORE_URLS = "http://localhost:$CarrierPort"
        CarrierClient__BaseUrl = "http://localhost:$ShopPort"
        CarrierClient__ApiKey = $demoKey
    }
    Write-Host "商城：http://localhost:$ShopPort/app/"
    Write-Host "物流商：http://localhost:$CarrierPort/"
    Write-Host "运行日志：$logPath"
    Write-Host '保持此终端运行，Ctrl+C 关闭本脚本启动的两个服务。密钥仅保存在进程环境中。'
    while (!$shop.HasExited -and !$carrier.HasExited) { Start-Sleep -Seconds 1 }
    throw "一个演示服务已经退出，请查看 $logPath 中的日志"
}
finally {
    # 仅终止本次创建且仍在运行的进程，不接管用户已有服务。
    foreach ($process in @($carrier, $shop)) {
        if ($null -ne $process -and !$process.HasExited) { $process.Kill($true); $process.WaitForExit() }
        if ($null -ne $process) { $process.Dispose() }
    }
    foreach ($handle in $logHandles) { try { $null = $handle.Copy.GetAwaiter().GetResult() } finally { $handle.Stream.Dispose() } }
}
