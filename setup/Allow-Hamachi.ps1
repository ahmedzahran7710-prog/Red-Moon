#requires -Version 5.1
[CmdletBinding(SupportsShouldProcess=$true)]
param([Parameter(Mandatory=$true)][string[]]$AllowedFriendAddress, [string]$Root = '')
# Resolve defaults only after parameter binding. Windows PowerShell 5.1 may
# not populate script automatic variables while evaluating param() defaults.
$scriptFile = $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($scriptFile)) {
    throw 'Run this saved script file, not its pasted contents; its directory is required.'
}
$scriptDirectory = [IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($scriptFile))
if ([string]::IsNullOrWhiteSpace($Root)) {
    $Root = [IO.Directory]::GetParent($scriptDirectory).FullName
}
. (Join-Path $scriptDirectory 'Common.ps1')
if (!$script:OnWindows) { throw 'Windows only.' }
$p = Read-Profile (Join-Path $Root '.local/active-profile.json')
foreach ($friend in $AllowedFriendAddress) {
    $ip = [Net.IPAddress]::Parse($friend)
    if ($ip.AddressFamily -ne [Net.Sockets.AddressFamily]::InterNetwork -or $friend -notlike '25.*') { throw 'Specify individual trusted Hamachi IPv4 peer addresses, not Any or a subnet.' }
}
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (!$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'This optional firewall operation needs an explicitly elevated PowerShell.' }
foreach ($row in @(@('LoginServer',[int]$p.LoginPort),@('MsgServer',[int]$p.GamePort))) {
    $name = "RedMoon-$($row[0])-$($row[1])"
    if (Get-NetFirewallRule -Name $name -ErrorAction SilentlyContinue) { throw "Rule $name already exists. Review it manually; it will not be silently replaced." }
    if ($PSCmdlet.ShouldProcess($name, 'Allow only specified peers, Hamachi local address, and game executable')) {
        New-NetFirewallRule -Name $name -DisplayName $name -Direction Inbound -Action Allow -Protocol TCP -LocalAddress $p.GameAddress -LocalPort $row[1] -RemoteAddress $AllowedFriendAddress -Program (Join-Path $Root "server/$($row[0]).exe") -Profile Any | Out-Null
    }
}
Write-Host 'No rule for MySQL 3306 or internal 9865 was created. No router/antivirus configuration was changed.'
