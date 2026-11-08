param([string]$BaseUrl='http://localhost:5000',[string]$PostgresContainer='washgo_postgres',[string]$PostgresUser='postgres',[string]$Database='washgo_db')
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly
$session=$null;$original=$null;$keys=@();$jobs=@()
try {
    $session=(Call-Api Post '/auth/login' @{email='admin@washgo.vn';password='Admin@123'} $null).data;$admin=$session.accessToken
    $original=(Call-Api Get '/system-setting' $null $admin).data
    if(!$original.version -or !$original.source){throw 'Missing settings metadata.'}
    Assert-Status 400 Put '/system-setting/commission' @{} $admin
    Assert-Status 400 Put '/system-setting/penalty' @{overdueFeeDay1=0} $admin
    Assert-Status 400 Put '/system-setting/commission' @{defaultCommissionRate=0} $admin
    $key=[Guid]::NewGuid().ToString();$keys+=$key
    $body=@{defaultCommissionRate=0;expectedVersion=$original.version;operationId=$key}
    $saved=(Call-Api Put '/system-setting/commission' $body $admin).data
    if($saved.defaultCommissionRate -ne 0 -or $saved.version -eq $original.version){throw 'Zero commission or revision failed.'}
    $again=(Call-Api Put '/system-setting/commission' $body $admin).data
    if($again.version -ne $saved.version -or $again.updatedAt -ne $saved.updatedAt){throw 'Replay changed saved result.'}
    $body.defaultCommissionRate=1
    Assert-Status 409 Put '/system-setting/commission' $body $admin
    Assert-Status 409 Put '/system-setting/commission' @{defaultCommissionRate=2;expectedVersion=$original.version} $admin
    $operation=(Call-Api Get "/system-setting/operations/$key" $null $admin).data
    if($operation.result.data.version -ne $saved.version){throw 'Operation lookup mismatch.'}
    foreach($rate in @(3,4)) {
        $key=[Guid]::NewGuid().ToString();$keys+=$key
        $jobs+=Start-Job -ScriptBlock {
            param($Url,$Token,$Version,$Key,$Rate)
            try{Invoke-RestMethod -Method Put -Uri "$Url/system-setting/commission" -Headers @{Authorization="Bearer $Token"} -ContentType 'application/json' -Body (@{expectedVersion=$Version;operationId=$Key;defaultCommissionRate=$Rate}|ConvertTo-Json)|Out-Null;200}
            catch{[int]$_.Exception.Response.StatusCode}
        } -ArgumentList $BaseUrl,$admin,$saved.version,$key,$rate
    }
    $codes=@($jobs|Receive-Job -Wait -AutoRemoveJob);$jobs=@()
    if((($codes|Sort-Object)-join ',') -ne '200,409'){throw "Concurrent updates: $codes"}
    $history=Call-Api Post '/system-setting/history/filter' @{page=1;take=10;action='UpdateCommission'} $admin
    if(!$history.succeeded){throw 'Settings history failed.'}
    Write-Output 'PASS: required fields, valid 0%, version mismatch, same-key replay, changed payload conflict, concurrent 200/409, durable lookup and history.'
} finally {
    foreach($job in $jobs){Stop-Job $job -ErrorAction SilentlyContinue;Remove-Job $job -Force -ErrorAction SilentlyContinue}
    if($original -and $admin){$current=(Call-Api Get '/system-setting' $null $admin).data;Call-Api Put '/system-setting/commission' @{defaultCommissionRate=$original.defaultCommissionRate;expectedVersion=$current.version} $admin|Out-Null}
    if($keys.Count){$ids=($keys|ForEach-Object{"'$_'"})-join ',';Sql "DELETE FROM washgo.administrative_operations WHERE id IN ($ids);"|Out-Null}
    if($session){Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $admin|Out-Null}
}
