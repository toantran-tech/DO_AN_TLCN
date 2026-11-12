param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$MailpitUrl='http://localhost:8025',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly
. "$PSScriptRoot/shipper-fixtures.ps1"

# LOCAL DEV ONLY: shipper avatar, rating sort over the whole team and the team summary.
$sessions=@{}; $teamId=$null; $lockerId=$null; $seedAvatar=$null; $seedShipperUser=$null
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$standardWash='b1111111-bbbb-1111-bbbb-111111111111'
function Names($Rows){ (@($Rows) | ForEach-Object { $_.fullName }) -join ',' }

try {
    foreach($role in @('Admin','Merchant','Customer')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$merchant=$sessions.Merchant.accessToken;$customer=$sessions.Customer.accessToken
    $sessions.Team=New-FixtureMerchant $run; $team=$sessions.Team.accessToken; $teamId=$sessions.Team.user.id
    $s=@{}
    foreach($n in 'A','B','C','D'){ $s[$n]=New-FixtureShipper $team "Team $run $n" $run }
    Sql @"
UPDATE washgo.shippers SET rating=4.5 WHERE id='$($s.A.id)';
UPDATE washgo.shippers SET rating=5.0, status=2 WHERE id='$($s.B.id)';
UPDATE washgo.shippers SET rating=0, status=3 WHERE id='$($s.C.id)';
UPDATE washgo.shippers SET rating=3.0, is_active=false WHERE id='$($s.D.id)';
UPDATE washgo.users SET avatar=to_jsonb('https://example.com/a-$run.png'::text) WHERE id='$($s.A.userId)';
"@ | Out-Null

    Write-Output '1. Team summary: two independent views, scoped by JWT'
    $sum=(Call-Api Get '/shipper/summary' $null $team).data
    if($sum.total -ne 4 -or $sum.working -ne 3 -or $sum.onLeave -ne 1 -or $sum.available -ne 2 -or $sum.busy -ne 1 -or $sum.offline -ne 1 -or !$sum.generatedAt){throw "Summary wrong: $($sum | ConvertTo-Json -Compress)"}
    if((Call-Api Get "/shipper/summary?merchantId=$teamId" $null $admin).data.total -ne 4){throw 'Admin summary by merchantId wrong.'}
    $seedTotal=(Call-Api Get '/shipper/summary' $null $merchant).data.total
    if((Call-Api Get "/shipper/summary?merchantId=$teamId" $null $merchant).data.total -ne $seedTotal){throw 'Merchant could read another team.'}
    if((Call-Api Post '/shipper/filter' @{keyword='zz-no-match';page=1;take=1} $team).total -ne 0 -or (Call-Api Get '/shipper/summary' $null $team).data.total -ne 4){throw 'Summary follows table filters.'}

    Write-Output '2. Shipper star sort retired: legacy Rating sort is ignored, default order kept'
    $default=(Call-Api Post '/shipper/filter' @{page=1;take=10} $team).list
    if((Names $default) -ne "Team $run A,Team $run B,Team $run C,Team $run D"){throw "Default order changed: $(Names $default)"}
    $asc=(Call-Api Post '/shipper/filter' @{page=1;take=10;sortBy=@(@{field='Rating';isDescending=$true})} $team).list
    if((Names $asc) -ne (Names $default)){throw "Legacy Rating sort not ignored: $(Names $asc)"}
    Assert-Status 400 Post '/shipper/filter' @{page=1;take=10;sortBy=@(@{field='CreatedAt';isDescending=$true})} $team

    Write-Output '3. Avatar on list/detail and on available shippers; unrated flag'
    $a=@($asc | Where-Object { $_.id -eq $s.A.id })[0]
    if($a.avatarUrl -ne "https://example.com/a-$run.png" -or !$a.hasRating){throw 'Avatar/hasRating missing on list.'}
    $c=(Call-Api Get "/shipper/$($s.C.id)" $null $team).data
    if($null -ne $c.avatarUrl -or $c.hasRating){throw 'Shipper without avatar/rating wrong.'}
    $seedShipperUser=Sql "SELECT id FROM washgo.users WHERE email='shipper@washgo.vn';"
    $seedAvatar=Sql "SELECT COALESCE(avatar::text,'') FROM washgo.users WHERE id='$seedShipperUser';"
    Sql "UPDATE washgo.users SET avatar=to_jsonb('https://example.com/seed-$run.png'::text) WHERE id='$seedShipperUser';" | Out-Null
    $locker=(Call-Api Post '/locker/' @{merchantId="$($sessions.Merchant.user.id)";name="Team Locker $run";address='Private development fixture';district='Test';city='Test';latitude=10.77;longitude=106.69;boxCount=1} $admin).data
    $lockerId=[Guid]$locker.id
    $l=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    $o=(Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$l.availableSlots[0].id;paymentMethod=2;items=@(@{serviceId=$standardWash;quantity=1})} $customer).data
    Invoke-TestDelivery $o.id deposit $customer | Out-Null
    $avail=(Call-Api Get "/wash-order/$($o.id)/available-shippers" $null $merchant).data
    if(@($avail | Where-Object { $_.avatarUrl -eq "https://example.com/seed-$run.png" }).Count -ne 1){throw 'Avatar missing on available shippers.'}

    Write-Output 'PASS: team summary (total/working/onLeave/available/busy/offline) in one count, scoped by JWT, Admin by merchantId, independent of table filters; legacy Rating sort ignored (star ratings retired), default order kept; other sort fields rejected; avatarUrl on list/detail/available shippers; hasRating flag.'
}
finally {
    foreach($session in $sessions.Values){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{Write-Warning 'Smoke session logout failed.'}}
    Remove-FixtureMerchant $teamId
    if($seedShipperUser){
        $value=if($seedAvatar){"'$seedAvatar'::jsonb"}else{'NULL'}
        Sql "UPDATE washgo.users SET avatar=$value WHERE id='$seedShipperUser';" | Out-Null
    }
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
