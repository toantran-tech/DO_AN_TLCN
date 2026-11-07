param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# LOCAL DEV ONLY: order detail shows name, address and coordinates of the deposit locker and of the pickup (return) locker.
$sessions=@{}; $lockerIds=@()
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)

try {
    foreach($role in @('Admin','Merchant','Customer')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$customer=$sessions.Customer.accessToken
    $merchantId=$sessions.Merchant.user.id
    $send=(Call-Api Post '/locker/' @{merchantId="$merchantId";name="Send Locker $run";address='1 Send Street';ward='Ward 1';district='Send District';city='Test';latitude=10.771;longitude=106.691;boxCount=2} $admin).data
    $back=(Call-Api Post '/locker/' @{merchantId="$merchantId";name="Pickup Locker $run";address='9 Pickup Road';district='Pickup District';city='Test';latitude=10.801;longitude=106.721;boxCount=2} $admin).data
    $lockerIds=@($send.id,$back.id)
    $serviceId=(Call-Api Post '/customer/services/filter' @{page=1;take=1} $customer).list[0].id
    $l=(Call-Api Get "/customer/lockers/$($send.id)" $null $customer).data

    Write-Output '1. Different return locker: its name, address and coordinates'
    $o=(Call-Api Post '/wash-order/' @{lockerId="$($send.id)";returnLockerId="$($back.id)";boxId=$l.availableSlots[0].id;paymentMethod=2;items=@(@{serviceId="$serviceId";quantity=1})} $customer).data
    $d=(Call-Api Get "/wash-order/$($o.id)" $null $customer).data
    if($d.returnLockerName -ne "Pickup Locker $run" -or $d.returnLockerAddress -ne '9 Pickup Road, Pickup District, Test' -or $d.returnLockerLatitude -ne 10.801 -or $d.returnLockerLongitude -ne 106.721){throw "Return locker info wrong: $($d.returnLockerName) / $($d.returnLockerAddress)"}
    if($d.lockerAddress -ne '1 Send Street, Ward 1, Send District, Test' -or $d.lockerLatitude -ne 10.771 -or $d.lockerLongitude -ne 106.691){throw 'Deposit locker info wrong.'}

    Write-Output '2. No return locker chosen: pickup at the deposit locker'
    $o2=(Call-Api Post '/wash-order/' @{lockerId="$($send.id)";boxId=$l.availableSlots[1].id;paymentMethod=2;items=@(@{serviceId="$serviceId";quantity=1})} $customer).data
    $d2=(Call-Api Get "/wash-order/$($o2.id)" $null $customer).data
    if($d2.returnLockerName -ne "Send Locker $run" -or $d2.returnLockerAddress -ne $d.lockerAddress -or $d2.returnLockerLatitude -ne 10.771){throw 'Default pickup locker wrong.'}

    Write-Output 'PASS: order detail has the deposit locker address/coordinates and the pickup locker name/address/coordinates (return locker or, by default, the deposit locker).'
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
}
