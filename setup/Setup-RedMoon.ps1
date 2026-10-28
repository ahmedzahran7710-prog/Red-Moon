[CmdletBinding()]
param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [string]$ProfilePath = (Join-Path $PSScriptRoot 'owner-profile.json'),
    [string]$MysqlExe = '',
    [switch]$CI
)
. (Join-Path $PSScriptRoot 'Common.ps1')
Assert-CiMode ([bool]$CI)
$Root = [IO.Path]::GetFullPath($Root)
$profile = Read-Profile $ProfilePath
$marker = Join-Path $Root '.local/installed.json'
if (Test-Path -LiteralPath $marker) {
    Write-Host 'Setup already completed. Existing schemas and accounts are not re-imported. Use Start-RedMoon.cmd.'
    return
}
$mysql = Find-Mysql $MysqlExe
$admin = Join-Path $Root 'server/RedMoon.Admin.exe'
if (!$script:OnWindows) { $admin = Join-Path $Root 'server/RedMoon.Admin' }
if (!(Test-Path -LiteralPath $admin)) { throw 'Ready-to-run executables are missing. Download the Windows bundle or run Build-RedMoon.ps1 first.' }
foreach ($file in @('account.sql','game.sql','fix_cq_levexp.sql','cq_action_mod.sql')) {
    if (!(Test-Path -LiteralPath (Join-Path $Root "database/$file"))) { throw 'Prepared installation data is missing. Do not import upstream seed accounts manually.' }
}
Write-Host 'New Red Moon installation. Existing databases/users will NEVER be reset or dropped.'
Write-Host "Database: 127.0.0.1:$($profile.DatabasePort). New schemas: $($profile.AccountDatabase), $($profile.GameDatabase)."
$password = if ($CI) { [string]$env:REDMOON_CI_DB_ROOT_PASSWORD } else { ConvertTo-Plain (Read-Host 'MariaDB root password (local only; not saved)' -AsSecureString) }
try {
    $version = Invoke-Mysql $mysql $profile $password 'SELECT VERSION();'
    if ($version -notmatch '^10\.4\.32-MariaDB') { throw "Expected the reviewed MariaDB 10.4.32 setup. Found $version. Stop and review compatibility, rather than silently modifying another server." }
    $existing = Invoke-Mysql $mysql $profile $password "SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME IN ('$($profile.AccountDatabase)','$($profile.GameDatabase)');"
    $userExists = Invoke-Mysql $mysql $profile $password "SELECT COUNT(*) FROM mysql.user WHERE User='$($profile.DatabaseUser)';"
    if ([int]$existing -ne 0 -or [int]$userExists -ne 0) { throw 'Target schema or runtime user already exists. Nothing was overwritten. Use a distinct installation profile or review it manually.' }
    $account = if ($CI) { 'ci_player' } else { Read-Host 'New game account name (3-16 letters, numbers or underscore)' }
    if ($account -notmatch '^[A-Za-z0-9_]{3,16}$') { throw 'Invalid game account name.' }
    $gamePassword = if ($CI) { [string]$env:REDMOON_CI_GAME_PASSWORD } else { ConvertTo-Plain (Read-Host 'New GAME password (8-16 English letters/digits, no symbols; not the DB password)' -AsSecureString) }
    $hash = Invoke-PrivateProcess $admin 'hash-password' ($gamePassword + "`n")
    $gamePassword = $null
    if ($hash -notmatch '^[A-Fa-f0-9]{128}$') { throw 'Unexpected password-hash format; database setup stopped.' }
    $secrets = @{
        REDMOON_DB_PASSWORD = (New-RandomHex 24)
        REDMOON_AUTH_PASSWORD = (New-RandomHex 8) # 16 ASCII bytes, the inter-server field limit
        REDMOON_TRANSFER_KEY = (New-RandomHex 32)
        REDMOON_TRANSFER_SALT = (New-RandomHex 32)
    }
    # Save generated application secrets before mutation so a partial failure can be
    # diagnosed locally. The ROOT password is never in this vault or on disk.
    Save-LocalSecrets $Root $secrets ([bool]$CI)
    Invoke-Mysql $mysql $profile $password "CREATE DATABASE $($profile.AccountDatabase) CHARACTER SET utf8mb4; CREATE DATABASE $($profile.GameDatabase) CHARACTER SET utf8mb4;" | Out-Null
    foreach ($pair in @(@('account.sql',$profile.AccountDatabase),@('game.sql',$profile.GameDatabase),@('fix_cq_levexp.sql',$profile.GameDatabase),@('cq_action_mod.sql',$profile.GameDatabase))) {
        Write-Host "Importing clean installation file: $($pair[0])"
        $sql = Get-Content -LiteralPath (Join-Path $Root "database/$($pair[0])") -Raw -Encoding UTF8
        try { Invoke-Mysql $mysql $profile $password $sql $pair[1] | Out-Null }
        catch { throw "Import stage '$($pair[0])' failed. $($_.Exception.Message)" }
    }
    $oldAccounts = Invoke-Mysql $mysql $profile $password 'SELECT COUNT(*) FROM account;' $profile.AccountDatabase
    $oldPlayers = Invoke-Mysql $mysql $profile $password 'SELECT COUNT(*) FROM cq_user;' $profile.GameDatabase
    if ([int]$oldAccounts -ne 0 -or [int]$oldPlayers -ne 0) { throw 'Clean seed verification failed. No old account should be used.' }
    $grants = @"
CREATE USER '$($profile.DatabaseUser)'@'127.0.0.1' IDENTIFIED BY '$($secrets.REDMOON_DB_PASSWORD)';
GRANT SELECT,INSERT,UPDATE,DELETE,EXECUTE ON $($profile.AccountDatabase).* TO '$($profile.DatabaseUser)'@'127.0.0.1';
GRANT SELECT,INSERT,UPDATE,DELETE,EXECUTE ON $($profile.GameDatabase).* TO '$($profile.DatabaseUser)'@'127.0.0.1';
"@
    Invoke-Mysql $mysql $profile $password $grants | Out-Null
    # Normal account by default; no automatic GM privileges, VIP boost or economy changes.
    Invoke-Mysql $mysql $profile $password "INSERT INTO account (name,password,type,vip) VALUES ('$account','$hash',2,0);" $profile.AccountDatabase | Out-Null
    Write-ServerConfiguration $Root $profile
    $profile | ConvertTo-Json -Depth 6 | ForEach-Object { Write-Utf8 (Join-Path $Root '.local/active-profile.json') $_ }
    $state = @{ SchemaVersion=1; DatabaseVersion=$version; AccountDatabase=$profile.AccountDatabase; GameDatabase=$profile.GameDatabase; OldAccountsImported=0; OldPlayersImported=0; CreatedNormalAccount=$true; InstalledUtc=[DateTime]::UtcNow.ToString('o') }
    Write-Utf8 $marker ($state | ConvertTo-Json)
    Write-Host 'SETUP_OK. Root password was not stored. Runtime credentials are local, not in GitHub.'
} finally {
    $password = $null; $gamePassword = $null; $hash = $null
}
