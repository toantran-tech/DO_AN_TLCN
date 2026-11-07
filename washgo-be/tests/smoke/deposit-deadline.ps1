param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# LOCAL DEV ONLY: deposit deadline fixed at creation and exposed to the Customer; expired-deposit notice.
# Temporarily changes the SLA deposit window and restores it in finally.
$sessions=@{}; $lockerId=$null; $original=$null
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)

function Set-Window($Minutes) {
    $b=@{depositWindowMinutes=$Minutes;merchantCollectSlaHours=$original.merchantCollectSlaHours;merchantReturnSlaHours=$original.merchantReturnSlaHours;freePickupHours=$original.freePickupHours}
    Call-Api Put '/system-setting/sla' $b $admin | Out-Null
}
function New-Order {
    $l=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    (Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$l.availableSlots[0].id;paymentMethod=2;items=@(@{serviceId="$serviceId";quantity=1})} $customer).data
}
function Order($Id) { (Call-Api Get "/wash-order/$Id" $null $customer).data }
function Notes($Id,$Type) { @((Call-Api Get '/notification?take=100' $null $customer).list | Where-Object { $_.linkId -eq "$Id" -and $_.type -eq $Type }) }
function Seconds($Iso) { ([DateTimeOffset]::Parse($Iso)).ToUnixTimeSeconds() }

try {
    foreach($role in @('Admin','Merchant','Customer')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$customer=$sessions.Customer.accessToken
    $original=(Call-Api Get '/system-setting' $null $admin).data
    $locker=(Call-Api Post '/locker/' @{merchantId="$($sessions.Merchant.user.id)";name="Deposit Deadline Locker $run";address='Private development fixture';district='Test';city='Test';latitude=10.77;longitude=106.69;boxCount=3} $admin).data
    $lockerId=[Guid]$locker.id
    $serviceId=(Call-Api Post '/customer/services/filter' @{page=1;take=1} $customer).list[0].id

    Write-Output '1. New order carries depositDeadline, window, serverNow and seconds left'
    Set-Window 20
    $o=New-Order
    $now=[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    if([Math]::Abs((Seconds $o.depositDeadline)-($now+1200)) -gt 90 -or $o.depositWindowMinutes -ne 20 -or !$o.serverNow -or $o.isDepositExpired -or $o.depositSecondsLeft -lt 1100){throw "Create response wrong: $($o.depositDeadline) $($o.depositWindowMinutes) $($o.depositSecondsLeft)"}
    $d=Order $o.id
    if($d.depositDeadline -ne $o.depositDeadline -or $d.depositWindowMinutes -ne 20){throw 'Detail deadline differs.'}
    $row=(Call-Api Post '/wash-order/mine' @{page=1;take=50} $customer).list | Where-Object { $_.id -eq $o.id }
    if($row.depositDeadline -ne $o.depositDeadline -or $row.depositSecondsLeft -le 0){throw 'List deadline missing.'}
    if((Call-Api Get '/policy/current' $null $null).data.rules.depositWindowMinutes -ne 20){throw 'Customer cannot read the deposit window.'}

    Write-Output '2. Admin changes the SLA: existing orders keep their deadline, new orders use the new window'
    Set-Window 45
    if((Order $o.id).depositDeadline -ne $o.depositDeadline){throw 'SLA change moved an existing deadline.'}
    $o2=New-Order
    if($o2.depositWindowMinutes -ne 45){throw 'New order ignored the new window.'}

    Write-Output '3. Past the deadline: flagged expired, deposit refused, late-cancel fee, no auto cancel'
    Sql "UPDATE washgo.orders SET deposit_deadline=now()-interval '1 minute' WHERE id='$($o.id)';" | Out-Null
    $e=Order $o.id
    if(!$e.isDepositExpired -or $e.depositSecondsLeft -ne 0){throw 'Expired flags wrong.'}
    Assert-Status 400 Post "/wash-order/$($o.id)/deposit" @{pinCode=$e.depositPinCode} $customer
    $quote=(Call-Api Get "/wash-order/$($o.id)/cancellation-quote" $null $customer).data
    if($quote.feePercent -ne $original.cancelLatePendingPercent){throw "Late-cancel percent wrong: $($quote.feePercent)"}

    Write-Output '4. Reminder scan: expired notice once (type 16, message key), reminder before the deadline'
    Sql "UPDATE washgo.orders SET created_on=extract(epoch FROM now() - interval '35 minutes')::bigint,deposit_deadline=now()+interval '10 minutes' WHERE id='$($o2.id)';" | Out-Null
    Call-Api Post '/notification/reminders/run' $null $admin | Out-Null
    Call-Api Post '/notification/reminders/run' $null $admin | Out-Null
    $expired=@(Notes $o.id 16)
    if($expired.Count -ne 1 -or $expired[0].messageKey -ne 'depositExpired.customer' -or $expired[0].messageArgs.orderCode -ne $o.orderCode){throw "Expired notice wrong: $($expired.Count)"}
    if(@(Notes $o.id 13).Count -ne 0){throw 'Expired order got a deposit reminder.'}
    $remind=@(Notes $o2.id 13)
    if($remind.Count -ne 1 -or $remind[0].messageArgs.minutesLeft -notin @('9','10') -or @(Notes $o2.id 16).Count -ne 0){throw 'Deposit reminder wrong.'}
    if((Order $o.id).status -ne 1){throw 'Expired order was cancelled automatically.'}
    Call-Api Post "/wash-order/$($o.id)/cancel" @{reason='Deadline smoke'} $customer | Out-Null
    Call-Api Post "/wash-order/$($o2.id)/cancel" @{reason='Deadline smoke'} $customer | Out-Null

    Write-Output 'PASS: depositDeadline/depositWindowMinutes/serverNow/isDepositExpired/depositSecondsLeft on create, detail and list; window readable through /policy/current; deadline fixed at creation across SLA changes; expired order refuses deposit and pays the late-cancel fee without auto cancel; expired notice sent once with message key; reminder before the deadline.'
}
finally {
    if($original){ try{ Set-Window $original.depositWindowMinutes }catch{ Write-Warning 'Could not restore the deposit window.' } }
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
}
