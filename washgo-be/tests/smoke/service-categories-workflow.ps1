param(
    [string]$BaseUrl = 'http://localhost:5000',
    [string]$PostgresContainer = 'washgo_postgres',
    [string]$PostgresUser = 'postgres',
    [string]$Database = 'washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly
if (([Uri]$BaseUrl).Host -notin @('localhost','127.0.0.1','::1')) { throw 'Local isolated fixture database required.' }
$run = 'CATEGORY_' + [Guid]::NewGuid().ToString('N')
$sessions=@{}; $categoryIds=@(); $serviceId=$null; $lockerId=$null; $orderId=$null
function Check($Condition,$Message) { if (!$Condition) { throw $Message } }
function New-Category($Suffix) {
    $body=@{operationId=[Guid]::NewGuid().ToString();code="${run}_$Suffix";name="${run}_$Suffix";isActive=$true;sortOrder=10}
    # Codes are limited to 30 characters, while names can hold the full fixture marker.
    $body.code='C_'+[Guid]::NewGuid().ToString('N').Substring(0,24)
    $result=(Call-Api Post '/admin/service-categories/' $body $admin).data
    $replay=(Call-Api Post '/admin/service-categories/' $body $admin).data
    Check ($result.id -eq $replay.id -and $result.version -eq $replay.version) 'Category create replay changed identity/version.'
    $result
}
function Category-Status($Row,$Active) {
    (Call-Api Put "/admin/service-categories/$($Row.id)/status" @{operationId=[Guid]::NewGuid().ToString();expectedVersion=$Row.version;isActive=$Active} $admin).data
}
try {
    foreach($role in @('Admin','Merchant','Customer')) {
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken; $merchant=$sessions.Merchant.accessToken; $customer=$sessions.Customer.accessToken
    Assert-Status 401 Post '/admin/service-categories/filter' @{} $null
    Assert-Status 403 Post '/admin/service-categories/filter' @{} $customer
    Assert-Status 403 Get '/notification/outbox/summary' $null $merchant
    $a=New-Category 'A'; $categoryIds+=$a.id
    $b=New-Category 'B'; $categoryIds+=$b.id
    $duplicate=@{operationId=[Guid]::NewGuid().ToString();code='D_'+[Guid]::NewGuid().ToString('N').Substring(0,24);name="  $($a.name.ToLower())  ";isActive=$true}
    Assert-Status 409 Post '/admin/service-categories/' $duplicate $admin
    Assert-Status 409 Put "/admin/service-categories/$($a.id)/status" @{operationId=[Guid]::NewGuid().ToString();expectedVersion='stale';isActive=$false} $admin
    $same=Category-Status $a $true
    Check ($same.version -eq $a.version) 'No-op category status changed its version.'
    $service=(Call-Api Post '/wash-service/' @{operationId=[Guid]::NewGuid().ToString();categoryId=$a.id;code='S_'+[Guid]::NewGuid().ToString('N').Substring(0,20);name=$run;unit='item';unitPrice=20000;pricePerItem=20000;serviceType=1;estimatedDurationHours=24} $admin).data
    $serviceId=$service.id
    Check ($service.categoryId -eq $a.id -and $service.categoryName -eq $a.name) 'Service category projection missing.'
    $visible=(Call-Api Post '/customer/services/filter' @{keyword=$run;categoryIds=@($a.id,$b.id);page=1;take=100} $customer).data
    Check (@($visible).Count -eq 1) 'Category OR filter omitted service.'
    $locker=(Call-Api Post '/locker/' @{merchantId=$sessions.Merchant.user.id;name=$run;address='Fixture';district='Test';city='Test';latitude=10.7;longitude=106.7;boxes=@(@{boxNumber='1';size=2})} $admin).data
    $lockerId=$locker.id
    $slot=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data.availableSlots[0]
    $order=(Call-Api Post '/wash-order/' @{lockerId=$lockerId;boxId=$slot.id;items=@(@{serviceId=$serviceId;quantity=1});paymentMethod=2} $customer).data
    $orderId=$order.id
    $view=(Call-Api Get "/wash-order/$orderId" $null $admin).data
    Check (@($view.timeline | Where-Object {$_.eventType -eq 'OrderCreated' -and $_.sequence -gt 0}).Count -eq 1) 'Created event missing structure/sequence.'
    # The category is the only classification: a new category has no legacy processing type, even if serviceType was sent.
    Check ((Sql "SELECT count(*) FROM washgo.order_items WHERE order_id='$orderId' AND category_id_snapshot='$($a.id)' AND processing_type_snapshot IS NULL;") -eq '1') 'Order did not snapshot the category (without a legacy processing type).'
    Check ((Sql "SELECT count(*) FROM washgo.notifications n JOIN washgo.order_status_history h ON n.event_id=h.id WHERE h.order_id='$orderId' AND n.user_id='$($sessions.Admin.user.id)';") -eq '1') 'Admin event fanout was lost or duplicated.'
    Check ((Sql "SELECT count(*) FROM washgo.notifications n LEFT JOIN washgo.notification_deliveries d ON d.notification_id=n.id WHERE n.link_id='$orderId' AND d.id IS NULL;") -eq '0') 'Inbox persisted without delivery outbox.'
    $a=(Call-Api Get "/admin/service-categories/$($a.id)" $null $admin).data
    Assert-Status 409 Post "/admin/service-categories/$($a.id)/delete" @{operationId=[Guid]::NewGuid().ToString();expectedVersion=$a.version;isActive=$false} $admin
    $a=Category-Status $a $false
    Assert-Status 404 Get "/customer/services/$serviceId" $null $customer
    Assert-Status 400 Post '/wash-order/' @{lockerId=$lockerId;boxId=$slot.id;items=@(@{serviceId=$serviceId;quantity=1});paymentMethod=2} $customer
    Check ((Sql "SELECT is_active FROM washgo.services WHERE id='$serviceId';") -eq 't') 'Category lock overwrote child service flag.'
    $oldName=Sql "SELECT category_name_snapshot FROM washgo.order_items WHERE order_id='$orderId';"
    Check ($oldName -eq $a.name) 'Category lock rewrote order snapshot.'
    Sql "UPDATE washgo.services SET is_active=false WHERE id='$serviceId';" | Out-Null
    $a=Category-Status $a $true
    Check ((Sql "SELECT is_active FROM washgo.services WHERE id='$serviceId';") -eq 'f') 'Opening category reactivated child service.'
    $batch=@{operationId=[Guid]::NewGuid().ToString();isActive=$false;items=@(@{id=$a.id;expectedVersion='stale'},@{id=$b.id;expectedVersion=$b.version})}
    $result=(Call-Api Post '/admin/service-categories/batch/status' $batch $admin).data
    $again=(Call-Api Post '/admin/service-categories/batch/status' $batch $admin).data
    Check (($result.items | ConvertTo-Json -Depth 10 -Compress) -eq ($again.items | ConvertTo-Json -Depth 10 -Compress)) 'Batch replay changed frozen item results.'
    $operation=(Call-Api Get "/admin/service-categories/operations/$($batch.operationId)" $null $admin).data
    Check (@($operation.items).Count -eq 2) 'Operation recovery omitted batch items.'
    $readId=Sql "SELECT n.id FROM washgo.notifications n JOIN washgo.order_status_history h ON h.id=n.event_id WHERE h.order_id='$orderId' AND n.user_id='$($sessions.Admin.user.id)' LIMIT 1;"
    # Simulate a durable retry after a read. Worker must suppress it, never recreate inbox.
    Sql "UPDATE washgo.notifications SET is_read=true WHERE id='$readId'; UPDATE washgo.notification_deliveries SET state='Pending',next_attempt_at=now(),lease_until=NULL,lease_token=NULL WHERE notification_id='$readId';" | Out-Null
    $suppressed=$false
    for($attempt=0;$attempt -lt 15;$attempt++) {
        if((Sql "SELECT state FROM washgo.notification_deliveries WHERE notification_id='$readId';") -eq 'Suppressed'){$suppressed=$true;break}
        Start-Sleep -Seconds 1
    }
    Check $suppressed 'Delivery worker did not suppress already-read inbox on retry.'
    Call-Api Get '/notification/outbox/summary' $null $admin | Out-Null
    Write-Output 'PASS: category CRUD/version/replay/dependencies, OR filters, effective availability, order snapshots/events, Admin fanout, durable outbox and read suppression.'
}
finally {
    if($orderId) {
        Sql "DELETE FROM washgo.notifications WHERE link_id='$orderId'; DELETE FROM washgo.order_status_history WHERE order_id='$orderId'; DELETE FROM washgo.order_items WHERE order_id='$orderId'; DELETE FROM washgo.orders WHERE id='$orderId';" | Out-Null
    }
    if($serviceId){Sql "DELETE FROM washgo.services WHERE id='$serviceId';" | Out-Null}
    if($lockerId){Sql "DELETE FROM washgo.locker_slots WHERE locker_id='$lockerId'; DELETE FROM washgo.lockers WHERE id='$lockerId';" | Out-Null}
    if($categoryIds.Count){$ids=($categoryIds|ForEach-Object {"'$_'"}) -join ',';Sql "DELETE FROM washgo.service_categories WHERE id IN ($ids);" | Out-Null}
    foreach($session in $sessions.Values){if($session.accessToken){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{Write-Warning 'Fixture session revocation failed.'}}}
}
