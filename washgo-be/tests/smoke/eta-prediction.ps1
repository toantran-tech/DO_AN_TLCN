param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# LOCAL DEV ONLY: ETA predicted from real processing times. A fixture Merchant gets three past orders whose history is
# written by SQL (deposit -> collect after 1/2/3 h, collect -> laundry done after 5/6/8 h); live orders are then predicted.
$sessions=@{}; $lockerId=$null; $history=@()
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$merchantB=[Guid]::NewGuid()
$standardWash='b1111111-bbbb-1111-bbbb-111111111111'

function New-Order {
    $l=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    (Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$l.availableSlots[0].id;paymentMethod=2;items=@(@{serviceId=$standardWash;quantity=1})} $customer).data
}
# Adds a status-history row at a given Unix time (cloned from the order's first row).
function Add-History($Id,$Status,$At) {
    Sql @"
INSERT INTO washgo.order_status_history SELECT (jsonb_populate_record(NULL::washgo.order_status_history,to_jsonb(h)||jsonb_build_object(
'id','$([Guid]::NewGuid())','to_status',$Status,'created_at',$At,'note','ETA smoke'))).* FROM washgo.order_status_history h WHERE order_id='$Id' ORDER BY created_at LIMIT 1;
"@ | Out-Null
}
function Eta($Id) { (Call-Api Get "/wash-order/$Id" $null $customer).data }
function Unix($Iso) { ([DateTimeOffset]::Parse($Iso)).ToUnixTimeSeconds() }

try {
    foreach($role in @('Admin','Merchant','Customer')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$customer=$sessions.Customer.accessToken
    $seedMerchantId=$sessions.Merchant.user.id
    Sql @"
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$merchantB','email','eta-m-$run@example.com','full_name','ETA Merchant','phone',NULL,'auth_provider',1,'provider_id',NULL,'is_active',true))).* FROM washgo.users u WHERE id='$seedMerchantId';
INSERT INTO washgo.merchants SELECT (jsonb_populate_record(NULL::washgo.merchants,to_jsonb(m)||jsonb_build_object(
'user_id','$merchantB','business_name','ETA Smoke Laundry','tax_code',NULL,'status',1,'max_capacity',100))).* FROM washgo.merchants m WHERE user_id='$seedMerchantId';
"@ | Out-Null
    $locker=(Call-Api Post '/locker/' @{merchantId="$merchantB";name="ETA Locker $run";address='Private development fixture';district='Test';city='Test';latitude=10.77;longitude=106.69;boxCount=6} $admin).data
    $lockerId=[Guid]$locker.id

    Write-Output '1. Past orders of this Merchant: processing 5/6/8 h, collect wait 1/2/3 h'
    $base=[DateTimeOffset]::UtcNow.AddDays(-2).ToUnixTimeSeconds()
    $i=0
    foreach($pair in @(@(1,5),@(2,6),@(3,8))){
        $h=New-Order; $history+=$h.id
        $t0=$base+$i*86400/4; $i++
        Sql "UPDATE washgo.orders SET status=12, merchant_id='$merchantB' WHERE id='$($h.id)';" | Out-Null
        Add-History $h.id 2 $t0
        Add-History $h.id 3 ($t0+$pair[0]*3600)
        Add-History $h.id 8 ($t0+$pair[0]*3600+$pair[1]*3600)
    }

    Write-Output '2. Deposited order: predicted from the deposit + median wait (2 h) + median processing (6 h)'
    $o=New-Order
    Invoke-TestDelivery $o.id deposit $customer | Out-Null
    $e=Eta $o.id
    $depositedAt=(@($e.timeline | Where-Object { $_.status -eq 2 }))[0].timestamp
    if($e.predictionStatus -ne 'predicted' -or $e.predictionSource -ne 'history-merchant' -or $e.predictionSampleSize -ne 3 -or $e.predictionLoadFactor -ne 1 -or !$e.predictionUpdatedAt){throw "Prediction meta wrong: $($e.predictionStatus)/$($e.predictionSource)/$($e.predictionSampleSize)"}
    if([Math]::Abs((Unix $e.predictedFinishAt)-($depositedAt+8*3600)) -gt 120){throw "Predicted time wrong: $($e.predictedFinishAt)"}
    if(!$e.estimatedFinishAt -and !$e.expectedDeliveryTime){throw 'Rule ETA missing.'}

    Write-Output '3. Collected: collect time + 6 h'
    $collected=[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    Sql "UPDATE washgo.orders SET status=3 WHERE id='$($o.id)';" | Out-Null
    Add-History $o.id 3 $collected
    $c=Eta $o.id
    if([Math]::Abs((Unix $c.predictedFinishAt)-($collected+6*3600)) -gt 120){throw "Collected prediction wrong: $($c.predictedFinishAt)"}

    Write-Output '4. Laundry done: actual time; cancelled: not applicable'
    $doneAt=$collected+3*3600
    Sql "UPDATE washgo.orders SET status=8 WHERE id='$($o.id)';" | Out-Null
    Add-History $o.id 8 $doneAt
    $d=Eta $o.id
    if($d.predictionStatus -ne 'done' -or $d.predictionSource -ne 'actual' -or (Unix $d.predictedFinishAt) -ne $doneAt){throw 'Done prediction wrong.'}
    $x=New-Order
    Call-Api Post "/wash-order/$($x.id)/cancel" @{reason='ETA smoke'} $customer | Out-Null
    if((Eta $x.id).predictionStatus -ne 'notApplicable'){throw 'Cancelled order predicted.'}

    Write-Output 'PASS: ETA predicted from the Merchant''s real history (median wait + median processing, sample size, load factor), deposit-based and collect-based predictions, actual finish time once done, not applicable when cancelled; rule ETA kept alongside.'
}
finally {
    foreach($session in $sessions.Values){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{Write-Warning 'Smoke session logout failed.'}}
    if($lockerId){
        Sql @"
BEGIN;
UPDATE washgo.orders SET status=99, is_deleted=true WHERE locker_id='$lockerId';
UPDATE washgo.locker_slots SET status=1,is_deleted=true WHERE locker_id='$lockerId';
UPDATE washgo.lockers SET available_slots=0,status=3,is_deleted=true WHERE id='$lockerId';
COMMIT;
"@ | Out-Null
    }
    Sql "UPDATE washgo.users SET is_active=false WHERE id='$merchantB'; UPDATE washgo.merchants SET status=3 WHERE user_id='$merchantB';" | Out-Null
}
