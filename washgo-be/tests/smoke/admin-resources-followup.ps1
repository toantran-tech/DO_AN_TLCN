param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# LOCAL DEV ONLY: batch outcomes, box status guard, Merchant cities, waiting-order media, promotion/service metadata, finance metadata.
$sessions=@{}; $promoId=$null
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
function StatusOf($Method,$Path,$Body,$Token){ try{ Call-Api $Method $Path $Body $Token | Out-Null; 200 } catch { [int]$_.Exception.Response.StatusCode } }

try {
    $sessions.Admin=(Call-Api Post '/auth/login' @{email='admin@washgo.vn';password='Admin@123'} $null).data; $admin=$sessions.Admin.accessToken

    Write-Output '1. Batch outcome: Changed, Unchanged, Skipped with totals'
    $p=(Call-Api Post '/promotion/' @{code="FUP_$run";discountType=1;discountValue=10;minOrderAmount=0;startAt='2025-01-01T00:00:00Z';endAt='2028-01-01T00:00:00Z';isActive=$true} $admin).data
    $promoId=$p.id
    if($p.canChangeCode -ne $true -or $p.changeCodeReasonCode){throw 'Unused promotion should allow code change.'}
    $b1=(Call-Api Post '/promotion/batch' @{operationId=[Guid]::NewGuid();action='Status';isActive=$false;items=@(@{id=$promoId;expectedVersion=$p.version},@{id=[Guid]::NewGuid();expectedVersion='x'})} $admin).data
    $mine=$b1.items | Where-Object { $_.id -eq $promoId }; $gone=$b1.items | Where-Object { $_.id -ne $promoId }
    if($mine.outcome -ne 'Changed' -or $gone.outcome -ne 'Skipped' -or $gone.reasonCode -ne 'NotFound' -or $b1.totals.changed -ne 1 -or $b1.totals.skipped -ne 1 -or $b1.totals.succeeded -ne 1){throw "Batch 1 wrong: $($b1 | ConvertTo-Json -Depth 4 -Compress)"}
    $v=(Call-Api Get "/promotion/$promoId" $null $admin).data.version
    $b2=(Call-Api Post '/promotion/batch' @{operationId=[Guid]::NewGuid();action='Status';isActive=$false;items=@(@{id=$promoId;expectedVersion=$v})} $admin).data
    if($b2.items[0].outcome -ne 'Unchanged' -or $b2.items[0].reasonCode -ne 'AlreadyInTargetState' -or $b2.totals.unchanged -ne 1 -or $b2.totals.changed -ne 0){throw "Batch 2 wrong: $($b2 | ConvertTo-Json -Depth 4 -Compress)"}
    $b3=(Call-Api Post '/promotion/batch' @{operationId=[Guid]::NewGuid();action='Status';isActive=$true;items=@(@{id=$promoId;expectedVersion='stale'})} $admin).data
    if($b3.items[0].outcome -ne 'Failed' -or $b3.items[0].reasonCode -ne 'VersionMismatch' -or $b3.totals.failed -ne 1){throw 'Stale item not Failed/VersionMismatch.'}
    $stats=(Call-Api Get "/promotion/$promoId/statistics" $null $admin).data
    if(-not $stats.generatedAt -or -not $stats.basis -or ([datetime]$stats.toDate - [datetime]$stats.fromDate).TotalDays -gt 31){throw 'Statistics metadata/default range wrong.'}
    Assert-Status 400 Get "/promotion/$promoId/statistics?fromDate=1600000000&toDate=1700000000" $null $admin

    Write-Output '2. Box status needs the box state the Admin saw'
    $lid=(Call-Api Post '/locker/filter' @{page=1;take=1} $admin).list[0].id
    $ld=(Call-Api Get "/locker/$lid" $null $admin).data
    $box=$ld.boxes | Where-Object { $_.status -eq 1 } | Select-Object -First 1
    if($box){
        if((StatusOf Put '/locker/box-status' @{lockerId=$lid;boxId=$box.id;newStatus=4;expectedVersion=$ld.version} $admin) -ne 400){throw 'Missing expectedBoxStatus accepted.'}
        if((StatusOf Put '/locker/box-status' @{lockerId=$lid;boxId=$box.id;newStatus=4;expectedVersion=$ld.version;expectedBoxStatus=4} $admin) -ne 409){throw 'Stale box status not 409.'}
    }

    Write-Output '3. Merchant cities, waiting-order media, service metadata'
    $cities=@((Call-Api Get '/merchant/cities' $null $admin).data)
    if($cities.Count -lt 1 -or ($cities | Select-Object -Unique).Count -ne $cities.Count){throw 'Cities wrong.'}
    $raw=(Invoke-WebRequest -UseBasicParsing -Method Post -Uri "$BaseUrl/order-assignment/filter" -Headers @{Authorization="Bearer $admin"} -ContentType 'application/json' -Body '{"page":1,"take":5}').Content
    if(($raw | ConvertFrom-Json).total -gt 0 -and ($raw -notmatch '"lockerImageUrls"' -or $raw -notmatch '"serviceItems"')){throw 'Waiting orders lack media fields.'}
    $sum=(Call-Api Get '/wash-service/summary' $null $admin).data
    if($null -eq $sum.pendingProposals -or $sum.counts.PendingProposals -ne $sum.pendingProposals){throw 'Service summary lacks pendingProposals.'}
    $svc=(Call-Api Post '/wash-service/filter' @{page=1;take=5} $admin).list
    if(@($svc | Where-Object { $null -eq $_.canDelete -or $null -eq $_.canEdit }).Count){throw 'Service metadata missing.'}

    Write-Output '4. Finance: hourly buckets, asOf, paged commissions, CSV offsets, line basis'
    $now=[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    $h=(Call-Api Post '/finance/revenue-report' @{fromDate=$now-86400;toDate=$now;granularity='hour'} $admin).data
    if($null -eq $h.hourly -or -not $h.asOf){throw 'Hourly report missing.'}
    Assert-Status 400 Post '/finance/revenue-report' @{fromDate=$now-30*86400;toDate=$now;granularity='hour'} $admin
    $asOf=(Call-Api Post '/finance/revenue-report' @{fromDate=$now-86400;toDate=$now;asOf=$now-3600} $admin).data
    if(([DateTimeOffset][datetime]$asOf.asOf).ToUnixTimeSeconds() -gt $now-3599){throw 'asOf not applied.'}
    $csv=(Call-Api Post '/finance/revenue-report/export' @{fromDate=$now-86400;toDate=$now} $admin)
    $csvText=if($csv.data){$csv.data}else{"$csv"}
    if($csvText -notmatch '\+07:00'){throw 'CSV period not in Vietnam time.'}
    $cp=(Call-Api Post '/finance/commissions/paged' @{fromDate=$now-30*86400;toDate=$now;page=1;take=2} $admin).data
    if($null -eq $cp.total -or @($cp.items).Count -gt 2 -or $null -eq $cp.commissionComplete){throw 'Paged commissions wrong.'}
    $st=(Call-Api Post '/finance/settlements/filter' @{page=1;take=1} $admin).list
    if(@($st).Count){
        $lines=Call-Api Get "/finance/settlements/$($st[0].id)/lines?take=5" $null $admin
        if($null -eq $lines.isSnapshot -or -not $lines.basis -or -not $lines.generatedAt){throw 'Settlement lines lack basis metadata.'}
    }

    Write-Output 'PASS: batch items report Changed/Unchanged/Skipped/Failed with reason codes and totals; promotion canChangeCode, bounded statistics with metadata; box status requires the observed box state (stale 409); Merchant cities distinct; waiting orders carry locker images and service thumbnails; service summary has pendingProposals in the same snapshot and list rows have action metadata; finance hourly buckets (<=48h), asOf, paged commissions with whole-period totals, CSV bounds with +07:00, settlement lines with isSnapshot/basis.'
}
finally {
    if($promoId){ Sql "UPDATE washgo.promotions SET is_deleted=true WHERE id='$promoId';" | Out-Null }
    foreach($session in $sessions.Values){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{}}
}
