param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# LOCAL DEV ONLY: the category is the only classification of services and proposals; serviceType is a legacy value
# derived from the five seed categories (null for new ones); ETA history is grouped by category.
$sessions=@{}; $run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$services=@(); $categories=@(); $lockerId=$null
$seedDryClean='ca000000-0000-0000-0000-000000000002'; $seedShoeCare='ca000000-0000-0000-0000-000000000003'

function Raw($Method,$Path,$Body,$Token){
    $request=@{UseBasicParsing=$true;Method=$Method;Uri="$BaseUrl$Path";Headers=@{Authorization="Bearer $Token"};TimeoutSec=30}
    if($null -ne $Body){ $request.ContentType='application/json; charset=utf-8'; $request.Body=[Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Depth 10)) }
    try { $r=Invoke-WebRequest @request; return @{Status=[int]$r.StatusCode;Json=($r.Content | ConvertFrom-Json)} }
    catch {
        if($null -eq $_.Exception.Response){throw}
        $json=if($_.ErrorDetails.Message){ try { $_.ErrorDetails.Message | ConvertFrom-Json } catch { $null } } else { $null }
        return @{Status=[int]$_.Exception.Response.StatusCode;Json=$json}
    }
}
function Service([hashtable]$Extra=@{}){
    $b=@{operationId=[Guid]::NewGuid().ToString();name="Category Smoke $run $([Guid]::NewGuid().ToString('N').Substring(0,6))";unit='item';unitPrice=30000;pricePerItem=30000;estimatedDurationHours=24}
    foreach($k in $Extra.Keys){$b[$k]=$Extra[$k]}
    $r=Raw Post '/wash-service/' $b $admin
    if($r.Json.data.id){ $script:services+=$r.Json.data.id }
    $r
}

try {
    foreach($role in @('Admin','Merchant','Customer')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$merchant=$sessions.Merchant.accessToken;$customer=$sessions.Customer.accessToken
    $cat=(Call-Api Post '/admin/service-categories/' @{operationId=[Guid]::NewGuid().ToString();name="Category Smoke $run";isActive=$true;sortOrder=50} $admin).data
    $categories+=$cat.id

    Write-Output '1. Admin services: the category alone classifies; serviceType is derived or null'
    $new=Service @{categoryId=$cat.id}
    if($new.Status -notin 200,201 -or $new.Json.data.categoryId -ne $cat.id -or $null -ne $new.Json.data.serviceType){throw "New-category service wrong: $($new.Status) $($new.Json.data.serviceType)"}
    $seed=Service @{categoryId=$seedDryClean}
    if($seed.Json.data.categoryId -ne $seedDryClean -or $seed.Json.data.serviceType -ne 2){throw 'Seed category did not give its legacy type.'}
    $both=Service @{categoryId=$cat.id;serviceType=2}
    if($both.Json.data.categoryId -ne $cat.id -or $null -ne $both.Json.data.serviceType){throw 'A sent serviceType overrode the category.'}
    $legacy=Service @{serviceType=3}
    if($legacy.Json.data.categoryId -ne $seedShoeCare -or $legacy.Json.data.serviceType -ne 3){throw 'Legacy serviceType-only client not mapped to its seed category.'}
    $none=Service
    if($none.Status -ne 400 -or $none.Json.meta.code -ne 'CategoryRequired'){throw "Missing category not rejected: $($none.Status) $($none.Json.meta.code)"}

    Write-Output '2. Edits: nothing sent keeps the pair; a new category drops the legacy type'
    $s=$seed.Json.data
    $kept=(Call-Api Put '/wash-service/' @{operationId=[Guid]::NewGuid().ToString();id=$s.id;expectedVersion=$s.version;name=$s.name;unit='item';unitPrice=31000;pricePerItem=31000;estimatedDurationHours=24} $admin).data
    if($kept.categoryId -ne $seedDryClean -or $kept.serviceType -ne 2 -or $kept.unitPrice -ne 31000){throw 'Edit without category changed the classification.'}
    $moved=(Call-Api Put '/wash-service/' @{operationId=[Guid]::NewGuid().ToString();id=$s.id;expectedVersion=$kept.version;categoryId=$cat.id;name=$s.name;unit='item';unitPrice=31000;pricePerItem=31000;estimatedDurationHours=24} $admin).data
    if($moved.categoryId -ne $cat.id -or $null -ne $moved.serviceType){throw 'Moving to a new category kept an unrelated legacy type.'}

    Write-Output '3. Filters: legacy serviceType filter means its seed category'
    $f=Call-Api Post '/wash-service/filter' @{serviceType=3;keyword="Category Smoke $run";page=1;take=50} $admin
    if($f.total -ne 1 -or $f.list[0].id -ne $legacy.Json.data.id){throw "Legacy type filter wrong: $($f.total)"}
    $byCategory=Call-Api Post '/wash-service/filter' @{categoryIds=@($cat.id);keyword="Category Smoke $run";page=1;take=50} $admin
    if($byCategory.total -ne 3){throw "Category filter wrong: $($byCategory.total)"}

    Write-Output '4. Merchant proposals: category only; approval keeps the category and no invented type'
    $pname="Category Proposal $run"
    Assert-Status 400 Post '/merchant-service/proposals' @{name="$pname X";description='Fixture';unitPrice=50000;estimatedDurationHours=24;unit='kg';reason='Fixture'} $merchant
    $p=(Call-Api Post '/merchant-service/proposals' @{name=$pname;description='Fixture';unitPrice=50000;estimatedDurationHours=24;categoryId=$cat.id;unit='kg';reason='Fixture'} $merchant).data
    if($p.proposed.categoryId -ne $cat.id -or $null -ne $p.proposed.serviceType -or $p.categoryMappingBasis -ne 'Captured'){throw 'Proposal classification wrong.'}
    $ok=(Call-Api Post "/service-proposal/$($p.id)/approve?expectedVersion=$($p.version)" $null $admin).data
    $services+=$ok.serviceId
    $row=Sql "SELECT category_id::text || '|' || COALESCE(category::text,'null') FROM washgo.services WHERE id='$($ok.serviceId)';"
    if($row -ne "$($cat.id)|null"){throw "Approved proposal stored $row."}

    Write-Output '5. ETA: a category with no history uses the rule, never another category''s history'
    $locker=(Call-Api Post '/locker/' @{merchantIds=@("$($sessions.Merchant.user.id)");name="Category Locker $run";address='Private development fixture';city='Test';latitude=10.77;longitude=106.69;boxCount=2} $admin).data
    $lockerId=$locker.id
    $l=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    $o=(Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$l.availableSlots[0].id;paymentMethod=2;items=@(@{serviceId=$new.Json.data.id;quantity=1})} $customer).data
    $item=Sql "SELECT category_id_snapshot::text || '|' || COALESCE(processing_type_snapshot::text,'null') FROM washgo.order_items WHERE order_id='$($o.id)';"
    if($item -ne "$($cat.id)|null"){throw "Order item snapshot $item."}
    Invoke-TestDelivery $o.id deposit $customer | Out-Null
    $eta=(Call-Api Get "/wash-order/$($o.id)" $null $customer).data
    if($eta.predictionSource -ne 'rule' -or !$eta.predictedFinishAt){throw "New-category order borrowed history: $($eta.predictionSource)"}

    Write-Output 'PASS: services and proposals are classified by categoryId only (CategoryRequired otherwise); serviceType is derived for the five seed categories and null for new ones, a sent serviceType never overrides the category, serviceType-only legacy clients map to the seed category; edits keep or re-derive the pair; legacy serviceType filters mean the seed category; approved proposals keep the category; order items snapshot the category and ETA for a category without history uses the rule.'
}
finally {
    foreach($session in $sessions.Values){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{Write-Warning 'Smoke session logout failed.'}}
    if($lockerId){
        Sql @"
BEGIN;
UPDATE washgo.orders SET status=99 WHERE locker_id='$lockerId' AND status NOT IN(12,99);
UPDATE washgo.locker_slots SET status=1,is_deleted=true WHERE locker_id='$lockerId';
UPDATE washgo.lockers SET available_slots=0,status=3,is_deleted=true WHERE id='$lockerId';
COMMIT;
"@ | Out-Null
    }
    foreach($id in $services | Where-Object { $_ }){ Sql "UPDATE washgo.services SET is_deleted=true, is_active=false WHERE id='$id';" | Out-Null }
    foreach($id in $categories){ Sql "UPDATE washgo.service_categories SET is_deleted=true, is_active=false WHERE id='$id';" | Out-Null }
}
