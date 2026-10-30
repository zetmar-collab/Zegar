function Get-AutostartDecision {
    param([bool]$IsUpgrade, [bool]$HasEntry, [bool]$Enable, [bool]$Disable)
    if ($Enable -and $Disable) { throw 'Wybierz tylko jedną opcję autostartu.' }
    if ($Disable) { return 'Disable' }
    if ($Enable -or $HasEntry -or -not $IsUpgrade) { return 'Enable' }
    return 'PreserveDisabled'
}
