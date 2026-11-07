param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# LOCAL DEV ONLY (Mock payment): receipt JSON and PDF for a paid order, access rules and a fixed receipt number.
$sessions=@{}; $lockerId=$null
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$customerB=[Guid]::NewGuid()
$standardWash='b1111111-bbbb-1111-bbbb-111111111111'

try {
    foreach($role in @('Admin','Merchant','Customer','Shipper')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$customer=$sessions.Customer.accessToken;$merchant=$sessions.Merchant.accessToken;$shipper=$sessions.Shipper.accessToken
    $shipperId=Sql "SELECT id FROM washgo.shippers WHERE user_id='$($sessions.Shipper.user.id)';"
    $seedCustomer=$sessions.Customer.user.id
    Sql @"
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$customerB','email','receipt-b-$run@example.com','phone',NULL,'auth_provider',1,'provider_id',NULL,'is_active',true))).* FROM washgo.users u WHERE id='$seedCustomer';
INSERT INTO washgo.customers SELECT (jsonb_populate_record(NULL::washgo.customers,to_jsonb(c)||jsonb_build_object('user_id','$customerB'))).* FROM washgo.customers c WHERE user_id='$seedCustomer';
"@ | Out-Null
    $sessions.B=(Call-Api Post '/auth/login' @{email="receipt-b-$run@example.com";password='Customer@123'} $null).data; $tokenB=$sessions.B.accessToken
    $locker=(Call-Api Post '/locker/' @{merchantId="$($sessions.Merchant.user.id)";name="Receipt Locker $run";address='Private development fixture';district='Test';city='Test';latitude=10.77;longitude=106.69;boxCount=2} $admin).data
    $lockerId=[Guid]$locker.id
    $l=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    $o=(Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$l.availableSlots[0].id;paymentMethod=2;items=@(@{serviceId=$standardWash;quantity=1})} $customer).data

    Write-Output '1. No receipt before payment'
    Assert-Status 400 Get "/wash-order/$($o.id)/receipt" $null $customer
    Assert-Status 400 Get "/wash-order/$($o.id)/receipt/pdf" $null $customer

    Invoke-TestDelivery $o.id deposit $customer | Out-Null
    Call-Api Put '/wash-order/assign-shipper' @{orderId=$o.id;shipperId="$shipperId"} $merchant | Out-Null
    Invoke-TestDelivery $o.id collect $shipper | Out-Null
    Call-Api Put "/wash-order/$($o.id)/actual-weight" @{weightKg=6} $merchant | Out-Null
    Call-Api Put '/wash-order/status' @{orderId=$o.id;newStatus=4} $merchant | Out-Null
    Call-Api Put '/wash-order/status' @{orderId=$o.id;newStatus=8} $merchant | Out-Null
    $pay=(Call-Api Post '/payment/create-url' @{orderId=$o.id;paymentMethod=2} $customer).data
    Call-Api Post '/payment/callback' @{orderId=$o.id;transactionReference=$pay.transactionReference;success=$true} $admin | Out-Null
    $order=(Call-Api Get "/wash-order/$($o.id)" $null $customer).data

    Write-Output '2. Receipt JSON: number, parties, lines, totals, payment'
    $r=(Call-Api Get "/wash-order/$($o.id)/receipt" $null $customer).data
    $merchantName=Sql "SELECT business_name FROM washgo.merchants WHERE user_id='$($sessions.Merchant.user.id)';"
    if($r.receiptNumber -ne ('BL'+$o.orderCode.Substring(2)) -or !$r.issuedAt -or $r.orderCode -ne $o.orderCode){throw 'Receipt number/date wrong.'}
    if($r.seller.name -ne $merchantName -or $r.buyer.name -ne $order.customerName -or $r.buyer.email -ne 'customer@washgo.vn'){throw 'Parties wrong.'}
    if($r.lines.Count -ne 1 -or $r.weightSurchargeAmount -ne $order.weightSurchargeAmount -or $r.laundryAmount -ne $order.finalAmount -or $r.totalPaid -ne $order.finalAmount){throw "Totals wrong: $($r.totalPaid) vs $($order.finalAmount)"}
    if($r.payments.Count -ne 1 -or $r.payments[0].method -ne 2 -or $r.payments[0].reference -ne $pay.transactionReference -or !$r.note -or $null -ne $r.vatRate){throw 'Payment/note wrong.'}

    Write-Output '3. Fixed number and date; access for Merchant/Admin, not for others'
    $again=(Call-Api Get "/wash-order/$($o.id)/receipt" $null $merchant).data
    if($again.receiptNumber -ne $r.receiptNumber -or $again.issuedAt -ne $r.issuedAt){throw 'Receipt number/date changed.'}
    if((Call-Api Get "/wash-order/$($o.id)/receipt" $null $admin).data.receiptNumber -ne $r.receiptNumber){throw 'Admin cannot read the receipt.'}
    Assert-Status 404 Get "/wash-order/$($o.id)/receipt" $null $tokenB
    Assert-Status 404 Get "/wash-order/$($o.id)/receipt" $null $shipper

    Write-Output '4. PDF file with the embedded Vietnamese font'
    $pdf=Invoke-WebRequest -UseBasicParsing -Uri "$BaseUrl/wash-order/$($o.id)/receipt/pdf" -Headers @{Authorization="Bearer $customer"}
    $bytes=$pdf.Content
    $head=[Text.Encoding]::ASCII.GetString($bytes,0,5)
    $text=[Text.Encoding]::ASCII.GetString($bytes)
    if($pdf.Headers['Content-Type'] -ne 'application/pdf' -or $head -ne '%PDF-' -or $bytes.Length -lt 2000 -or $text -notmatch 'BeVietnamPro'){throw "PDF wrong: $($pdf.Headers['Content-Type']) $head $($bytes.Length)"}
    if($pdf.Headers['Content-Disposition'] -notmatch "bien-lai-$($r.receiptNumber)\.pdf"){throw 'PDF file name wrong.'}

    Write-Output 'PASS: no receipt before payment; receipt with fixed number/date, Merchant seller and Customer buyer, service lines, surcharge, totals equal to the paid amount, payment reference and non-VAT note; readable by owner/Merchant/Admin only; A5 PDF download with the embedded Be Vietnam Pro font.'
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
    Sql "UPDATE washgo.users SET is_active=false WHERE id='$customerB';" | Out-Null
}
