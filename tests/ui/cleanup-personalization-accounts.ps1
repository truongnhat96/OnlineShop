param([Parameter(Mandatory=$true)][string]$AccountFile)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$allowed = [IO.Path]::GetFullPath((Join-Path $workspace 'docs/ui-review/personalization')) + [IO.Path]::DirectorySeparatorChar
$file = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $AccountFile).Path)
if (-not $file.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($file) -notmatch '^main-test-accounts-[0-9]+\.json$') { throw 'Unexpected account file.' }
$accounts = @(Get-Content -LiteralPath $file -Raw | ConvertFrom-Json)
# Windows PowerShell may wrap the JSON array as one object.
if ($accounts.Count -eq 1 -and $accounts[0] -is [array]) { $accounts = @($accounts[0]) }
if ($accounts.Count -ne 2 -or @($accounts | Select-Object -Unique).Count -ne 2 -or @($accounts | Where-Object { $_ -notmatch '^PersonalTest[0-9]{13,20}[23]$' }).Count -ne 0) { throw 'Unexpected test account list.' }
$connection = [System.Data.SqlClient.SqlConnection]::new('Server=.\SQLEXPRESS;Database=OnlineShop;Integrated Security=True;TrustServerCertificate=True;')
$connection.Open()
$transaction = $connection.BeginTransaction()
try {
    foreach ($username in $accounts) {
        $cmd = $connection.CreateCommand(); $cmd.Transaction = $transaction
        $cmd.CommandText = @'
SELECT u.Id FROM dbo.Users u
WHERE u.Username=@username AND u.Email=@email AND u.DisplayName=N'Test' AND u.RoleId=2
AND NOT EXISTS (SELECT 1 FROM dbo.CartItems c WHERE c.UserId=u.Id)
AND NOT EXISTS (SELECT 1 FROM dbo.Reviews r WHERE r.UserId=u.Id)
AND NOT EXISTS (SELECT 1 FROM dbo.Posts p WHERE p.UserId=u.Id)
AND NOT EXISTS (SELECT 1 FROM dbo.DiscountUsages d WHERE d.UserId=u.Id)
AND NOT EXISTS (SELECT 1 FROM dbo.RecommendationEvents e WHERE e.UserId=u.Id)
AND NOT EXISTS (SELECT 1 FROM dbo.RecommendationPreferences s WHERE s.UserId=u.Id AND s.Enabled=1)
'@
        [void]$cmd.Parameters.Add('@username',[System.Data.SqlDbType]::NVarChar,100); $cmd.Parameters['@username'].Value = $username
        [void]$cmd.Parameters.Add('@email',[System.Data.SqlDbType]::NVarChar,50); $cmd.Parameters['@email'].Value = "$username@example.invalid"
        $id = $cmd.ExecuteScalar(); $cmd.Dispose()
        if ($null -eq $id) { throw "Test account not found or still has activity; no deletion performed." }
        $delete = $connection.CreateCommand(); $delete.Transaction = $transaction
        $delete.CommandText = 'DELETE FROM dbo.Users WHERE Id=@id'
        [void]$delete.Parameters.Add('@id',[System.Data.SqlDbType]::Int); $delete.Parameters['@id'].Value = [int]$id
        if ($delete.ExecuteNonQuery() -ne 1) { throw 'Unexpected deletion count.' }
        $delete.Dispose()
    }
    $transaction.Commit()
    Write-Output 'Removed exactly two cleared test accounts.'
} catch { $transaction.Rollback(); throw } finally { $transaction.Dispose(); $connection.Dispose() }
