param([string]$BaseUrl='http://localhost:5000',[string]$PostgresContainer='washgo_postgres',[string]$PostgresUser='postgres',[string]$Database='washgo_db')
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly
# Local development: dedicated locker/orders; restores the settings even on failure.
$sessions=@();$locker=$null;$original=$null;$orders=@()
function Save-Sla($Settings,$Minutes,$Collect,$Return) {
    $current=(Call-Api Get '/system-setting' $null $admin).data
    (Call-Api Put '/system-setting/sla' @{expectedVersion=$current.version;depositWindowMinutes=$Minutes;merchantCollectSlaHours=$Collect;merchantReturnSlaHours=$Return;freePickupHours=$Settings.freePickupHours} $admin).data
}
function New-FixtureOrder {
    $box=(Call-Api Get "/customer/lockers/$($locker.id)" $null $customer).data.availableSlots[0]
    (Call-Api Post '/wash-order/' @{lockerId=$locker.id;boxId=$box.id;paymentMethod=2;items=@(@{serviceId="$service";quantity=1})} $customer).data
}
try {
    $a=(Call-Api Post '/auth/login' @{email='admin@washgo.vn';password='Admin@123'} $null).data;$sessions+=$a;$admin=$a.accessToken
    $c=(Call-Api Post '/auth/login' @{email='customer@washgo.vn';password='Customer@123'} $null).data;$sessions+=$c;$customer=$c.accessToken
    $merchant=Sql "SELECT user_id FROM washgo.merchants WHERE status=1 LIMIT 1;"
    $service=Sql "SELECT id FROM washgo.services WHERE is_active AND NOT is_deleted AND status=2 LIMIT 1;"
    $run=[Guid]::NewGuid().ToString('N')
    $locker=(Call-Api Post '/locker/' @{merchantId="$merchant";code="POL_$run";name="Policy Fixture $run";address='Development fixture';district='Test';city='Test';latitude=10.77;longitude=106.69;boxes=@(@{boxNumber='1';size=2},@{boxNumber='2';size=2})} $admin).data
    $original=(Call-Api Get '/system-setting' $null $admin).data
    Save-Sla $original 30 4 24|Out-Null
    $first=New-FixtureOrder;$orders+=$first.id
    $before=Sql "SELECT operational_settings_json::text FROM washgo.orders WHERE id='$($first.id)';"
    Save-Sla $original 45 6 36|Out-Null
    $second=New-FixtureOrder;$orders+=$second.id
    $unchanged=(Call-Api Get "/wash-order/$($first.id)" $null $customer).data
    if($first.depositDeadline -ne $unchanged.depositDeadline -or $second.depositWindowMinutes -ne 45 -or $unchanged.depositWindowMinutes -ne 30){throw 'Published settings changed existing deposit deadline or new order ignored new policy.'}
    $after=Sql "SELECT operational_settings_json::text FROM washgo.orders WHERE id='$($first.id)';"
    if($before -ne $after){throw 'Existing operational policy was overwritten.'}
    $snapshots=Sql "SELECT collect_sla_hours_snapshot||':'||return_sla_hours_snapshot FROM washgo.orders WHERE id='$($second.id)';"
    if($snapshots -ne '6:36'){throw "New SLA snapshots incorrect: $snapshots"}
    Write-Output 'PASS: existing deadline/policy remain unchanged; new orders adopt 45-minute deposit and 6/36-hour SLA; settings restored.'
} finally {
    if($original){Save-Sla $original $original.depositWindowMinutes $original.merchantCollectSlaHours $original.merchantReturnSlaHours|Out-Null}
    foreach($id in $orders){try{Call-Api Post "/wash-order/$id/cancel" @{reason='Policy smoke cleanup'} $customer|Out-Null}catch{Write-Warning 'Fixture cancellation needs inspection.'}}
    if($locker){Sql "UPDATE washgo.locker_slots SET is_deleted=true WHERE locker_id='$($locker.id)'; UPDATE washgo.lockers SET is_deleted=true,status=3 WHERE id='$($locker.id)';"|Out-Null}
    foreach($session in $sessions){Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}
}
