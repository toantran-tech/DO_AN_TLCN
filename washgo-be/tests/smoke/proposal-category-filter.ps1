param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# LOCAL DEV ONLY: POST /service-proposal/filter with categoryIds filters by the category of the proposed content (new and
# update proposals, legacy serviceType-only content), never by the live category of the service; counted before paging.
$sessions=@{}; $run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$services=@(); $categories=@()
$seedDryClean='ca000000-0000-0000-0000-000000000002'

function Check($Condition,$Message){ if(!$Condition){throw $Message} }
function Find-Proposals([hashtable]$Body,$Token){
    $b=@{keyword=$run;page=1;take=50}; foreach($k in $Body.Keys){$b[$k]=$Body[$k]}
    Call-Api Post '/service-proposal/filter' $b $Token
}
function Ids($Result){ @($Result.list | ForEach-Object id) }
function Proposal($Path,$Name,$CategoryId){
    (Call-Api Post $Path @{name=$Name;description='Fixture';unitPrice=40000;estimatedDurationHours=24;categoryId=$CategoryId;unit='kg';reason='Fixture'} $merchant).data
}

try {
    foreach($role in @('Admin','Merchant')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken; $merchant=$sessions.Merchant.accessToken
    $a=(Call-Api Post '/admin/service-categories/' @{operationId=[Guid]::NewGuid().ToString();name="Proposal Filter A $run";isActive=$true;sortOrder=60} $admin).data
    $b=(Call-Api Post '/admin/service-categories/' @{operationId=[Guid]::NewGuid().ToString();name="Proposal Filter B $run";isActive=$true;sortOrder=61} $admin).data
    $categories+=$a.id,$b.id
    $s=(Call-Api Post '/wash-service/' @{operationId=[Guid]::NewGuid().ToString();name="Proposal Filter Service $run";unit='item';unitPrice=30000;pricePerItem=30000;estimatedDurationHours=24;categoryId=$b.id} $admin).data
    $services+=$s.id

    Write-Output '1. New and update proposals are filtered by the proposed category, not the live category of the service'
    $pNew=Proposal '/merchant-service/proposals' "Proposal Filter New $run" $a.id
    # An edit proposal captures the category of the service at proposal time (B), whatever the Merchant sends.
    $pUpdate=Proposal "/merchant-service/$($s.id)/proposals" "Proposal Filter Update $run" $a.id
    Check ($pUpdate.proposed.categoryId -eq $b.id) 'Edit proposal did not capture the service category.'
    $pLegacy=Proposal '/merchant-service/proposals' "Proposal Filter Legacy $run" $seedDryClean
    # Legacy content (serviceType only): same backfill statement as migration AddProposalCategoryFilter.
    Sql @"
UPDATE washgo.service_change_requests SET proposed_data = (proposed_data - 'categoryId') || '{"serviceType":2}', proposed_category_id = NULL WHERE id='$($pLegacy.id)';
UPDATE washgo.service_change_requests SET proposed_category_id = CASE
    WHEN jsonb_typeof(proposed_data->'categoryId') = 'string' AND (proposed_data->>'categoryId') ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' THEN (proposed_data->>'categoryId')::uuid
    WHEN jsonb_typeof(proposed_data->'serviceType') = 'number' AND (proposed_data->>'serviceType') IN ('1','2','3','4','5') THEN ('ca000000-0000-0000-0000-' || lpad(proposed_data->>'serviceType', 12, '0'))::uuid
END WHERE id='$($pLegacy.id)';
"@ | Out-Null
    # The service later moves to A: the filter keeps using the proposed content (B), never the live category.
    Sql "UPDATE washgo.services SET category_id='$($a.id)' WHERE id='$($s.id)';" | Out-Null
    $byA=Find-Proposals @{categoryIds=@($a.id)} $admin
    Check ($byA.total -eq 1 -and @(Ids $byA)[0] -eq $pNew.id) "Category A (new proposal only): total $($byA.total)"
    $byB=Find-Proposals @{categoryIds=@($b.id)} $admin
    Check ($byB.total -eq 1 -and @(Ids $byB)[0] -eq $pUpdate.id) "Category B must match the captured edit proposal: total $($byB.total)"
    $mine=Find-Proposals @{categoryIds=@($a.id,$b.id)} $merchant
    Check ($mine.total -eq 2) "Merchant scope with category: total $($mine.total)"

    Write-Output '2. Combined with requestType/merchantId/keyword, legacy content, paging after filtering'
    $updates=Find-Proposals @{categoryIds=@($a.id,$b.id);requestType=2} $admin
    Check ($updates.total -eq 1 -and @(Ids $updates)[0] -eq $pUpdate.id) 'requestType + category wrong.'
    $legacy=Find-Proposals @{categoryIds=@($seedDryClean)} $admin
    Check ($legacy.total -eq 1 -and @(Ids $legacy)[0] -eq $pLegacy.id) "Legacy proposal not found by its seed category: total $($legacy.total)"
    Check ((Call-Api Get "/service-proposal/$($pLegacy.id)" $null $admin).data.proposed.categoryId -eq $seedDryClean) 'DTO and filter disagree on the legacy category.'
    $page=Find-Proposals @{categoryIds=@($a.id,$b.id,$seedDryClean);take=1} $admin
    Check ($page.total -eq 3 -and @($page.list).Count -eq 1 -and $page.hasNextPage) "Paging after the category filter: total $($page.total)"
    $page2=Find-Proposals @{categoryIds=@($a.id,$b.id,$seedDryClean);take=1;page=3} $admin
    Check (@($page2.list).Count -eq 1 -and !$page2.hasNextPage) 'Last page wrong.'
    Check ((Find-Proposals @{categoryIds=@($a.id,$b.id);merchantId="$($sessions.Merchant.user.id)"} $admin).total -eq 2) 'merchantId + category wrong.'
    Check ((Find-Proposals @{categoryIds=@($a.id);merchantId=[Guid]::NewGuid().ToString()} $admin).total -eq 0) 'Other merchantId still matched.'
    Check ((Find-Proposals @{categoryIds=@($a.id);keyword="Proposal Filter New $run"} $admin).total -eq 1) 'keyword + category wrong.'
    Check ((Find-Proposals @{categoryIds=@()} $admin).total -eq 3) 'Empty categoryIds must mean all.'

    Write-Output '3. Inactive category still matches its proposals; invalid ids rejected'
    Sql "UPDATE washgo.service_categories SET is_active=false WHERE id='$($a.id)';" | Out-Null
    Check ((Find-Proposals @{categoryIds=@($a.id)} $admin).total -eq 1) 'Inactive category lost its proposals.'
    Assert-Status 400 Post '/service-proposal/filter' @{categoryIds=@([Guid]::Empty.ToString());page=1;take=10} $admin
    Assert-Status 400 Post '/service-proposal/filter' @{categoryIds=@(1..101 | ForEach-Object { [Guid]::NewGuid().ToString() });page=1;take=10} $admin

    Write-Output 'PASS: proposals filtered by the category of the proposed content (new proposals, edit proposals keep the category captured at proposal time even after the service moves), legacy serviceType content via its seed category, combined with requestType/merchantId/keyword, counted and paged after filtering, inactive category kept, empty list = all, invalid ids 400.'
}
finally {
    foreach($session in $sessions.Values){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{}}
    try {
        Sql "DELETE FROM washgo.service_change_requests WHERE proposed_name LIKE 'Proposal Filter % $run';" | Out-Null
        foreach($id in $services | Where-Object { $_ }){ Sql "UPDATE washgo.services SET is_deleted=true, is_active=false WHERE id='$id';" | Out-Null }
        foreach($id in $categories | Where-Object { $_ }){ Sql "UPDATE washgo.service_categories SET is_deleted=true, is_active=false WHERE id='$id';" | Out-Null }
    } catch { Write-Warning "Proposal filter cleanup failed: $($_.Exception.Message)" }
}
