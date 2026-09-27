$ErrorActionPreference = 'Stop'
dotnet build "$PSScriptRoot/SmtcCoverUiRegression.csproj" --no-restore -p:Platform=x64 -v:q -clp:ErrorsOnly
if ($LASTEXITCODE -ne 0) { throw 'SMTC cover UI regression build failed' }
$testExe = Join-Path $PSScriptRoot 'bin/x64/Debug/net11.0-windows10.0.26100.0/win-x64/SmtcCoverUiRegression.exe'
$testOutput = Split-Path $testExe
$fixtures = Join-Path $PSScriptRoot '../SmtcCoverRegression/bin/Release/net11.0-windows10.0.26100.0/fixtures'
Copy-Item -LiteralPath $fixtures -Destination $testOutput -Recurse -Force
$resultPath = Join-Path $testOutput 'result.txt'
if (Test-Path -LiteralPath $resultPath) { Remove-Item -LiteralPath $resultPath }
$testProcess = Start-Process -FilePath $testExe -WindowStyle Hidden -PassThru
if (-not $testProcess.WaitForExit(30000)) {
    Stop-Process -Id $testProcess.Id
    throw 'SMTC cover UI regression timed out'
}
$result = Get-Content -LiteralPath $resultPath -Raw
Write-Output $result
if ($testProcess.ExitCode -ne 0 -or -not $result.StartsWith('PASS:')) { throw 'SMTC cover UI regression failed' }
