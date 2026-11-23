param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# LOCAL DEV ONLY: required structured opening hours and the collect deadline counted in opening hours from DepositedAt.
# Merchant X collects only on one weekday two days ahead (08:00-20:00), so every deposit now waits for that shift.
$NoAutoAssign=$true
$sessions=@{}; $lockerIds=@(); $orders=@()
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$mX=[Guid]::NewGuid(); $mY=[Guid]::NewGuid()
$vnToday=[DateTime]::UtcNow.AddHours(7).Date
$openDay=$vnToday.AddDays(2)
$iso=if($openDay.DayOfWeek -eq [DayOfWeek]::Sunday){7}else{[int]$openDay.DayOfWeek}
$scheduleX="{""timeZone"":""Asia/Ho_Chi_Minh"",""alwaysOpen"":false,""closedDates"":[],""weekly"":[{""dayOfWeek"":$iso,""shifts"":[{""open"":""08:00"",""close"":""20:00""}]}]}"

function Raw($Method,$Path,$Body,$Token){
    $request=@{UseBasicParsing=$true;Method=$Method;Uri="$BaseUrl$Path";Headers=@{Authorization="Bearer $Token"};TimeoutSec=30}
    if($null -ne $Body){ $request.ContentType='application/json; charset=utf-8'; $request.Body=[Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Depth 10)) }
    try { $r=Invoke-WebRequest @request; return @{Status=[int]$r.StatusCode;Json=($r.Content | ConvertFrom-Json)} }
    catch {
        if($null -eq $_.Exception.Response){throw}
        $text=$_.ErrorDetails.Message
        if(!$text){ try { $text=(New-Object IO.StreamReader($_.Exception.Response.GetResponseStream())).ReadToEnd() } catch { } }
        $json=if($text){ try { $text | ConvertFrom-Json } catch { $null } } else { $null }
        return @{Status=[int]$_.Exception.Response.StatusCode;Json=$json}
    }
}
function Check($Condition,$Message){ if(!$Condition){throw $Message} }
function Utc($Value){ ([DateTimeOffset]::Parse("$Value", [Globalization.CultureInfo]::InvariantCulture)).UtcDateTime }
function Same($Value,[DateTime]$Expected){ $null -ne $Value -and [math]::Abs(((Utc $Value)-$Expected).TotalSeconds) -lt 2 }
function Merchant-Sql($Id,$Name,$Schedule){
    $value=if($Schedule){"'$Schedule'::jsonb"}else{'NULL'}
@"
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$Id','email','deadline-$Id@example.com','full_name','$Name','phone',NULL,'auth_provider',1,'provider_id',NULL,'is_active',true))).* FROM washgo.users u WHERE id='$seedMerchantId';
INSERT INTO washgo.merchants SELECT (jsonb_populate_record(NULL::washgo.merchants,to_jsonb(m)||jsonb_build_object(
'user_id','$Id','business_name','$Name','tax_code',NULL,'status',1,'contract_end_date',NULL,'opening_hours',NULL))).* FROM washgo.merchants m WHERE user_id='$seedMerchantId';
UPDATE washgo.merchants SET opening_schedule=$value WHERE user_id='$Id';
"@
}
function New-Order {
    $l=(Call-Api Get "/customer/lockers/$($lockerIds[0])" $null $customer).data
    $o=(Call-Api Post '/wash-order/' @{lockerId="$($lockerIds[0])";boxId=$l.availableSlots[0].id;paymentMethod=2;items=@(@{serviceId="$serviceId";quantity=1})} $customer).data
    Invoke-TestDelivery $o.id deposit $customer | Out-Null
    $script:orders+=$o.id
    $o
}
function Order($Id){ (Call-Api Get "/wash-order/$Id" $null $admin).data }
function Merchant-Body($Id,[hashtable]$Extra){
    $p=(Call-Api Get "/merchant/$Id" $null $admin).data
    $b=@{id="$Id";expectedVersion=$p.version;businessName=$p.businessName;email=$p.email;phone=$p.phone;address=$p.address;city=$p.city;district=$p.district;latitude=$p.latitude;longitude=$p.longitude;maxCapacity=$p.maxCapacity;serviceRadiusKm=$p.serviceRadiusKm}
    foreach($k in $Extra.Keys){$b[$k]=$Extra[$k]}
    $b
}

try {
    foreach($role in @('Admin','Merchant','Customer')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken; $customer=$sessions.Customer.accessToken
    $seedMerchantId=$sessions.Merchant.user.id
    $serviceId=(Call-Api Post '/customer/services/filter' @{page=1;take=1} $customer).list[0].id

    Write-Output '1. Opening hours are required and must be computable'
    $base=@{businessName="Deadline $run";address='1 Test Street';city='Test';district='Test';phone='0912345678';latitude=10.77;longitude=106.69}
    function Create($Suffix,[hashtable]$Extra){ $b=$base.Clone(); $b.email="deadline-$Suffix-$run@example.test"; $b.clientRequestId=[Guid]::NewGuid().ToString(); foreach($k in $Extra.Keys){$b[$k]=$Extra[$k]}; Raw Post '/merchant/' $b $admin }
    $missing=Create 'none' @{}
    Check ($missing.Status -eq 400 -and $missing.Json.meta.code -eq 'OpeningHoursRequired' -and $missing.Json.errors.openingSchedule) "Missing hours: $($missing.Status) $($missing.Json.meta.code)."
    Check ((Create 'blank' @{openingHours='   '}).Json.meta.code -eq 'OpeningHoursRequired') 'Blank hours accepted.'
    Check ((Create 'free' @{openingHours='morning until late'}).Json.meta.code -eq 'OpeningHoursInvalidFormat') 'Free text accepted.'
    $overlap=@{weekly=@(@{dayOfWeek=1;shifts=@(@{open='08:00';close='12:00'},@{open='11:00';close='14:00'})})}
    Check ((Create 'overlap' @{openingSchedule=$overlap}).Json.meta.code -eq 'OpeningHoursOverlap') 'Overlap accepted.'
    Check ((Create 'same' @{openingSchedule=@{weekly=@(@{dayOfWeek=1;shifts=@(@{open='08:00';close='08:00'})})}}).Json.meta.code -eq 'OpeningHoursInvalidShift') 'open == close accepted.'
    Check ((Create 'noday' @{openingSchedule=@{weekly=@()}}).Json.meta.code -eq 'OpeningHoursNoOpenDay') 'No open day accepted.'
    $ok=Create 'ok' @{openingSchedule=@{weekly=@(1..5 | ForEach-Object { @{dayOfWeek=$_;shifts=@(@{open='08:00';close='12:00'},@{open='13:00';close='20:00'})} });closedDates=@('2026-12-25')}}
    Check ($ok.Status -eq 201 -and @($ok.Json.data.openingSchedule.weekly).Count -eq 5 -and $ok.Json.data.openingSchedule.timeZone -eq 'Asia/Ho_Chi_Minh' -and $ok.Json.data.openingHours -like '*08:00-12:00, 13:00-20:00*') "Structured create failed: $($ok.Json.message)"
    $created=$ok.Json.data.id
    $keep=Raw Put '/merchant/' (Merchant-Body $created @{}) $admin
    Check ($keep.Status -eq 200 -and @($keep.Json.data.openingSchedule.weekly).Count -eq 5) 'Edit without hours must keep the schedule.'
    $legacy=Create 'legacy' @{openingHours='07:00-22:00'}
    Check ($legacy.Status -eq 201 -and @($legacy.Json.data.openingSchedule.weekly).Count -eq 7) 'Exact "HH:mm-HH:mm" becomes a daily schedule.'

    Write-Output '2. Deadline counts opening hours from the deposit, fixed at Merchant assignment'
    Sql ((Merchant-Sql $mX "Deadline $run X" $scheduleX) + (Merchant-Sql $mY "Deadline $run Y" $null)) | Out-Null
    $locker=(Call-Api Post '/locker/' @{merchantIds=@("$mX","$mY");name="Deadline $run";address='Private development fixture';district='Test';city='Test';latitude=10.77;longitude=106.69;boxCount=4} $admin).data
    $lockerIds+=$locker.id
    $o1=New-Order
    $d=Order $o1.id
    Check ($d.collectionDeadlineState -eq 'AwaitingMerchant' -and $null -eq $d.collectionDeadlineAt) "Before assignment: $($d.collectionDeadlineState)."
    Call-Api Post "/order-assignment/$($o1.id)/assign" @{merchantId="$mX"} $admin | Out-Null
    $d=Order $o1.id
    $sla=[int]$d.collectSlaHoursSnapshot
    Check ($sla -ge 1 -and $sla -le 12) "Test expects a collect SLA within one 12-hour shift, got $sla."
    $start=$openDay.AddHours(8).AddHours(-7)
    Check ($d.collectionDeadlineState -eq 'Scheduled' -and (Same $d.collectionStartsAt $start) -and (Same $d.collectionDeadlineAt $start.AddHours($sla))) "Deadline wrong: $($d.collectionStartsAt) / $($d.collectionDeadlineAt) (sla $sla)."
    Check (-not $d.isCollectionOverdue) 'A night deposit is not overdue before the shop opens.'
    $promised=$d.collectionDeadlineAt
    $customerView=(Call-Api Get "/wash-order/$($o1.id)" $null $customer).data
    Check (Same $customerView.collectionDeadlineAt (Utc $promised)) 'Customer sees the same deadline.'
    Check ((Raw Put '/merchant/' (Merchant-Body $mX @{openingSchedule=@{alwaysOpen=$true}}) $admin).Status -eq 200) 'Schedule edit failed.'
    Check (Same (Order $o1.id).collectionDeadlineAt (Utc $promised)) 'Editing the schedule must not move a promised deadline.'

    Write-Output '3. Merchant without opening hours: no fake deadline, computed from the deposit once hours are added'
    $o2=New-Order
    Call-Api Post "/order-assignment/$($o2.id)/assign" @{merchantId="$mY"} $admin | Out-Null
    $d2=Order $o2.id
    Check ($d2.collectionDeadlineState -eq 'ScheduleMissing' -and $null -eq $d2.collectionDeadlineAt) "Missing schedule: $($d2.collectionDeadlineState)."
    $noHours=Raw Put '/merchant/' (Merchant-Body $mY @{}) $admin
    Check ($noHours.Status -eq 400 -and $noHours.Json.meta.code -eq 'OpeningHoursRequired') 'Editing a Merchant without hours must require them.'
    Check ((Raw Put '/merchant/' (Merchant-Body $mY @{openingSchedule=@{alwaysOpen=$true}}) $admin).Status -eq 200) 'Adding hours failed.'
    $d2=Order $o2.id
    Check ($d2.collectionDeadlineState -eq 'Scheduled' -and (Same $d2.collectionDeadlineAt ((Utc $d2.depositedAt).AddHours([int]$d2.collectSlaHoursSnapshot)))) "Recomputed from DepositedAt: $($d2.collectionDeadlineAt) vs $($d2.depositedAt)."

    Write-Output '4. Overdue checks use the stored deadline only'
    Sql "UPDATE washgo.orders SET deposited_at=now() AT TIME ZONE 'utc' - interval '10 hours' WHERE id='$($o1.id)';" | Out-Null
    $alerts=(Call-Api Get '/admin/alerts?type=2&take=100' $null $admin).data.items
    Check (-not ($alerts | Where-Object resourceId -eq $o1.id)) 'Deposit + 4h passed at night must not alert while the deadline is ahead.'
    Sql "UPDATE washgo.orders SET collection_deadline_at=now() AT TIME ZONE 'utc' - interval '1 hours' WHERE id='$($o1.id)';" | Out-Null
    $alerts=(Call-Api Get '/admin/alerts?type=2&take=100' $null $admin).data.items
    Check (@($alerts | Where-Object resourceId -eq $o1.id).Count -eq 1) 'Past stored deadline must alert.'
    Check ((Order $o1.id).isCollectionOverdue) 'Detail flags overdue from the stored deadline.'

    Write-Output 'PASS: opening hours required with stable codes (missing/blank/free text/overlap/open==close/no day), structured and exact legacy input, edit keeps schedule; deadline counted in opening hours from DepositedAt and fixed at assignment, unchanged by schedule edits, ScheduleMissing until hours are added then computed from the deposit; alerts use the stored deadline.'
}
finally {
    foreach($session in $sessions.Values){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{Write-Warning 'Smoke session logout failed.'}}
    foreach($lockerId in $lockerIds){
        Sql @"
BEGIN;
UPDATE washgo.orders SET status=99 WHERE locker_id='$lockerId' AND status NOT IN(12,99);
UPDATE washgo.locker_slots SET status=1,is_deleted=true WHERE locker_id='$lockerId';
UPDATE washgo.lockers SET available_slots=0,status=3,is_deleted=true WHERE id='$lockerId';
COMMIT;
"@ | Out-Null
    }
    Sql "UPDATE washgo.users SET is_active=false WHERE id IN('$mX','$mY') OR email LIKE 'deadline-%-$run@example.test'; UPDATE washgo.merchants SET status=3 WHERE user_id IN('$mX','$mY') OR business_name='Deadline $run';" | Out-Null
}
