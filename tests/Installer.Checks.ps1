$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../scripts/AutostartPolicy.ps1')
$cases = @(
    @($false,$false,$false,$false,'Enable'),
    @($true,$false,$false,$false,'PreserveDisabled'),
    @($true,$true,$false,$false,'Enable'),
    @($true,$false,$true,$false,'Enable'),
    @($false,$false,$false,$true,'Disable'),
    @($true,$true,$false,$true,'Disable')
)
foreach ($case in $cases) {
    $actual = Get-AutostartDecision -IsUpgrade $case[0] -HasEntry $case[1] -Enable $case[2] -Disable $case[3]
    if ($actual -ne $case[4]) { throw "Autostart policy failed: $actual" }
}
$rejected = $false
try { Get-AutostartDecision -IsUpgrade $true -HasEntry $false -Enable $true -Disable $true } catch { $rejected = $true }
if (-not $rejected) { throw 'Conflicting switches were accepted.' }
Write-Output 'PASS: 7 installer policy checks (no registry modifications).'
