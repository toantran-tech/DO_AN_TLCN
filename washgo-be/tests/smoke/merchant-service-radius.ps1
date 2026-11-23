param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# LOCAL DEV ONLY: Merchant service radius is a hard rule for Merchant–locker links.
# Fixture Merchants on one meridian (lng 106.2): A at 11.5 (radius 5 km), B 10 km north of A (radius 8 km),
# C without a store position. Along a meridian the Haversine distance is exactly R·Δφ, so positions are given in km from A.
$sessions=@{}; $lockerIds=@()
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$mA=[Guid]::NewGuid(); $mB=[Guid]::NewGuid(); $mC=[Guid]::NewGuid()
$lat0=11.5; $lng=106.2; $deg=180/([math]::PI*6371)
function At([double]$Km){ [math]::Round($lat0+$Km*$deg,9) }

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
function Merchant-Sql($Id,$Name,$Lat,$Lng,$Radius){
@"
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$Id','email','radius-$Id@example.com','full_name','$Name','phone',NULL,'auth_provider',1,'provider_id',NULL,'is_active',true))).* FROM washgo.users u WHERE id='$seedMerchantId';
INSERT INTO washgo.merchants SELECT (jsonb_populate_record(NULL::washgo.merchants,to_jsonb(m)||jsonb_build_object(
'user_id','$Id','business_name','$Name','tax_code',NULL,'status',1,'latitude',$Lat,'longitude',$Lng,'service_radius_km',$Radius,'contract_end_date',NULL))).* FROM washgo.merchants m WHERE user_id='$seedMerchantId';
"@
}
function Eligible([double]$Km,[string]$Extra=''){ (Raw Get "/locker/eligible-merchants?latitude=$(At $Km)&longitude=$lng&keyword=Radius%20$run$Extra" $null $admin) }
function Row($Result,$Id){ @($Result.Json.list | Where-Object { $_.merchantId -eq "$Id" })[0] }
function Locker-Body([double]$Km,$MerchantIds,[hashtable]$Extra=@{}){
    $b=@{name="Radius $run $Km";address='Private development fixture';district='Test';city='Test';latitude=(At $Km);longitude=$lng;lockerType=1;boxCount=2}
    if($null -ne $MerchantIds){ $b.merchantIds=@($MerchantIds | ForEach-Object { "$_" }) }
    foreach($k in $Extra.Keys){$b[$k]=$Extra[$k]}
    $b
}
function Count-Lockers([double]$Km){ [int](Sql "SELECT count(*) FROM washgo.lockers WHERE name='Radius $run $Km' AND NOT is_deleted;") }

try {
    foreach($role in @('Admin','Merchant','Customer')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken; $customer=$sessions.Customer.accessToken
    $seedMerchantId=$sessions.Merchant.user.id
    Sql ((Merchant-Sql $mA "Radius $run A" $lat0 $lng 5) + (Merchant-Sql $mB "Radius $run B" (At 10) $lng 8) + (Merchant-Sql $mC "Radius $run C" 0 0 50)) | Out-Null

    Write-Output '1. Eligible Merchants by locker position (store → locker, own radius, unrounded)'
    $three=Eligible 3
    Check ($three.Status -eq 200 -and (@($three.Json.list | ForEach-Object merchantId) -join ',') -eq "$mA,$mB") "3 km: expected A then B, got $(@($three.Json.list | ForEach-Object businessName) -join ',')."
    Check ((Row $three $mA).distanceKm -eq 3 -and (Row $three $mB).distanceKm -eq 7 -and (Row $three $mA).serviceRadiusKm -eq 5 -and $three.Json.meta.eligibleCount -eq 2) 'Distances/radius wrong at 3 km.'
    $six=Eligible 6 '&includeIneligible=true'
    Check ((Row $six $mB).eligible -and -not (Row $six $mA).eligible -and (Row $six $mA).blockedReasonCode -eq 'MerchantOutsideServiceRadius') '6 km: only B is eligible.'
    Check ((Row $six $mC).blockedReasonCode -eq 'MerchantLocationUnavailable' -and $null -eq (Row $six $mC).distanceKm) 'Merchant without a position must not count as 0 km.'
    Check (@($six.Json.list)[0].merchantId -eq "$mB") 'Eligible Merchants come first.'
    $over=Eligible 5.004 '&includeIneligible=true'
    Check ((Row $over $mA).distanceKm -eq 5 -and -not (Row $over $mA).eligible) '5.004 km shows 5.00 but is outside 5 km.'
    $under=Eligible 4.996
    Check ((Row $under $mA).eligible) '4.996 km is inside 5 km.'
    $none=Eligible -6
    Check ($none.Status -eq 200 -and $none.Json.total -eq 0 -and $none.Json.meta.code -eq 'NoEligibleMerchantForLocker') 'No eligible Merchant south of A.'
    Check ((Raw Get "/locker/eligible-merchants?latitude=0&longitude=0" $null $admin).Json.meta.code -eq 'LockerLocationRequired') '0,0 is not a locker position.'
    Check ((Raw Get "/locker/eligible-merchants?latitude=$(At 3)&longitude=$lng" $null $sessions.Merchant.accessToken).Status -eq 403) 'Admin only.'

    Write-Output '2. Locker create rejects out-of-range Merchants as a whole'
    $bad=Raw Post '/locker/' (Locker-Body 6 @($mB,$mA)) $admin
    Check ($bad.Status -eq 400 -and $bad.Json.meta.code -eq 'MerchantOutsideServiceRadius' -and @($bad.Json.meta.merchants).Count -eq 1 -and $bad.Json.meta.merchants[0].merchantId -eq "$mA" -and $bad.Json.meta.merchants[0].serviceRadiusKm -eq 5) "Out-of-range create: $($bad.Status) $($bad.Json.meta.code)."
    Check ((Count-Lockers 6) -eq 0) 'Rejected create must not save a locker.'
    $noLoc=Raw Post '/locker/' (Locker-Body 3 @($mC)) $admin
    Check ($noLoc.Status -eq 400 -and $noLoc.Json.meta.code -eq 'MerchantLocationUnavailable') 'Merchant without position cannot take a locker.'
    $empty=Raw Post '/locker/' (Locker-Body -6 @()) $admin
    Check ($empty.Status -eq 400 -and $empty.Json.meta.code -eq 'NoEligibleMerchantForLocker') 'No eligible Merchant: clear code.'
    $edge=Raw Post '/locker/' (Locker-Body 5.004 @($mA)) $admin
    Check ($edge.Status -eq 400 -and $edge.Json.meta.code -eq 'MerchantOutsideServiceRadius') '5.004 km must be rejected for a 5 km radius.'
    $created=Raw Post '/locker/' (Locker-Body 3 @($mA,$mB)) $admin
    Check ($created.Status -eq 201) "In-range create failed: $($created.Json.message)"
    $lockerId=$created.Json.data.id; $lockerIds+=$lockerId
    $onlyB=Raw Post '/locker/' (Locker-Body 6 @($mB)) $admin
    Check ($onlyB.Status -eq 201) 'B alone may take the 6 km locker.'
    $lockerIds+=$onlyB.Json.data.id

    Write-Output '3. Locker edits re-check every Merchant, also when the list is kept'
    $ld=(Call-Api Get "/locker/$lockerId" $null $admin).data
    $move=Raw Put '/locker/' (Locker-Body 6 $null @{id=$lockerId;expectedVersion=$ld.version;name=$ld.name;status=1}) $admin
    Check ($move.Status -eq 400 -and $move.Json.meta.code -eq 'MerchantOutsideServiceRadius' -and $move.Json.meta.merchants[0].merchantId -eq "$mA") 'Moving out of A''s radius with merchantIds = null must fail.'
    $after=(Call-Api Get "/locker/$lockerId" $null $admin).data
    Check ($after.version -eq $ld.version -and [math]::Abs($after.latitude-(At 3)) -lt 1e-9) 'Rejected move changed the locker.'
    $rename=Raw Put '/locker/' (Locker-Body 3 $null @{id=$lockerId;expectedVersion=$ld.version;name="$($ld.name) renamed";status=1}) $admin
    Check ($rename.Status -eq 200) "Rename with same position failed: $($rename.Json.message)"
    $ld=(Call-Api Get "/locker/$lockerId" $null $admin).data
    $linked=Raw Get "/locker/eligible-merchants?lockerId=$lockerId&keyword=Radius%20$run" $null $admin
    Check ((Row $linked $mA).isLinked -and (Row $linked $mA).linkPriority -eq 1 -and (Row $linked $mB).linkPriority -eq 2) 'Edit view lists linked Merchants with priority.'

    Write-Output '4. Merchant radius/location changes that would exclude a linked locker are blocked'
    $profile=(Call-Api Get "/merchant/$mA" $null $admin).data
    $body=@{id="$mA";expectedVersion=$profile.version;businessName=$profile.businessName;email=$profile.email;phone=$profile.phone;address=$profile.address;city=$profile.city;district=$profile.district;latitude=$profile.latitude;longitude=$profile.longitude;maxCapacity=$profile.maxCapacity;serviceRadiusKm=2}
    $shrink=Raw Put '/merchant/' $body $admin
    Check ($shrink.Status -eq 409 -and $shrink.Json.meta.code -eq 'MerchantCoverageWouldExcludeLockers' -and @($shrink.Json.meta.lockers | Where-Object lockerId -eq $lockerId).Count -eq 1 -and $shrink.Json.meta.suggestedServiceRadiusKm -ge 3) "Radius shrink: $($shrink.Status) $($shrink.Json.meta.code)."
    Check ((Call-Api Get "/merchant/$mA" $null $admin).data.serviceRadiusKm -eq 5) 'Blocked edit changed the Merchant.'
    $body.serviceRadiusKm=4
    Check ((Raw Put '/merchant/' $body $admin).Status -eq 200) 'A radius that still covers the locker (4 km ≥ 3 km) is accepted.'
    $mToken=(Call-Api Post '/auth/login' @{email="radius-$mA@example.com";password='Merchant@123'} $null).data; $sessions["A"]=$mToken
    $request=(Call-Api Post '/merchant-profile/update-requests' @{businessName="Radius $run A";address='Moved store';city='Test';phone='0901234567';openingHours='08:00-20:00';latitude=(At -6);longitude=$lng} $mToken.accessToken).data
    $approve=Raw Post "/merchant-profile-request/$($request.id)/approve?expectedVersion=$($request.version)" $null $admin
    Check ($approve.Status -eq 409 -and $approve.Json.meta.code -eq 'MerchantCoverageWouldExcludeLockers') "Approving a store move out of range: $($approve.Status) $($approve.Json.meta.code)."
    Check ([math]::Abs((Call-Api Get "/merchant/$mA" $null $admin).data.latitude-$lat0) -lt 1e-9) 'Blocked approval moved the store.'
    Check ([int](Sql "SELECT status FROM washgo.merchant_profile_requests WHERE id='$($request.id)';") -eq 1) 'Blocked approval must leave the request pending.'

    Write-Output '5. Legacy out-of-range links: kept, reported, not used for new orders or the catalog'
    # Simulate data saved before the rule: the locker sits 5.5 km south of A (radius 4) and 15.5 km from B (radius 8).
    $legacy=(Call-Api Post '/locker/' (Locker-Body 9.5 @($mB)) $admin).data; $lockerIds+=$legacy.id
    Sql "INSERT INTO washgo.locker_merchants(id,locker_id,merchant_id,priority,created_at) VALUES('$([Guid]::NewGuid())','$($legacy.id)','$mA',2,now()); UPDATE washgo.lockers SET latitude=$(At -5.5) WHERE id='$($legacy.id)';" | Out-Null
    $issues=(Call-Api Get '/locker/coverage-issues?take=100' $null $admin).list | Where-Object lockerId -eq $legacy.id
    Check (@($issues).Count -eq 2 -and -not @($issues)[0].lockerStillServed -and @($issues | Where-Object merchantId -eq "$mA")[0].reasonCode -eq 'MerchantOutsideServiceRadius') 'Coverage issues must list both broken links of the legacy locker.'
    Check ([int](Sql "SELECT count(*) FROM washgo.locker_merchants WHERE locker_id='$($legacy.id)';") -eq 2) 'Legacy links must not be deleted.'
    $catalog=Raw Get "/customer/lockers?keyword=Radius%20$run&take=50" $null $customer
    $names=@($catalog.Json.list | ForEach-Object name)
    Check (($names -contains "$($ld.name)") -and -not ($names -contains $legacy.name)) "Catalog must hide lockers no Merchant covers: $($names -join ', ')."
    Check ((Raw Get "/customer/lockers/$($legacy.id)" $null $customer).Status -eq 404) 'Detail of an unserved locker is not public.'
    $serviceId=(Call-Api Post '/customer/services/filter' @{page=1;take=1} $customer).list[0].id
    $box=@((Call-Api Get "/locker/$($legacy.id)" $null $admin).data.boxes)[0].id
    $order=Raw Post '/wash-order/' @{lockerId="$($legacy.id)";boxId="$box";paymentMethod=2;items=@(@{serviceId="$serviceId";quantity=1})} $customer
    Check ($order.Status -eq 400 -and [int](Sql "SELECT count(*) FROM washgo.orders WHERE locker_id='$($legacy.id)';") -eq 0) "Order creation must refuse a locker no Merchant covers: $($order.Status)."

    Write-Output 'PASS: eligible list by locker position (own radius, unrounded, linked kept), create/edit reject out-of-range or position-less Merchants without partial writes, Merchant radius/location edits and profile approvals blocked when a linked locker would fall outside, legacy links kept and reported but not served.'
}
finally {
    foreach($session in $sessions.Values){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{Write-Warning 'Smoke session logout failed.'}}
    foreach($id in $lockerIds){ Sql "UPDATE washgo.locker_slots SET is_deleted=true WHERE locker_id='$id'; UPDATE washgo.lockers SET status=3,is_deleted=true WHERE id='$id';" | Out-Null }
    Sql "UPDATE washgo.merchant_profile_requests SET status=3 WHERE merchant_id IN('$mA','$mB','$mC') AND status=1; UPDATE washgo.users SET is_active=false WHERE id IN('$mA','$mB','$mC'); UPDATE washgo.merchants SET status=3 WHERE user_id IN('$mA','$mB','$mC');" | Out-Null
}
