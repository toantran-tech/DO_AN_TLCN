param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# LOCAL DEV ONLY: two private lockers of the seed Merchant at known coordinates (UC 1.2.2 distance).
$sessions=@{}; $lockerIds=@()
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)

function Lockers([string]$Query) { Call-Api Get "/customer/lockers?keyword=Distance%20$run$Query" $null $customer }

try {
    foreach($role in @('Admin','Merchant','Customer')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$customer=$sessions.Customer.accessToken
    $merchantId=$sessions.Merchant.user.id
    # "A" is about 1.11 km north of the customer, "B" sits on the customer's position.
    foreach($l in @(@{name="Distance $run A";lat=12.01},@{name="Distance $run B";lat=12.0})){
        $created=(Call-Api Post '/locker/' @{merchantId="$merchantId";name=$l.name;address='Private development fixture';district='Test';city='Test';latitude=$l.lat;longitude=105.0;boxCount=1} $admin).data
        $lockerIds+=$created.id
    }

    Write-Output '1. Without a position: no distance, sorted by name'
    $plain=Lockers ''
    if($plain.total -ne 2 -or $plain.list[0].name -ne "Distance $run A" -or $null -ne $plain.list[0].distanceKm){throw 'Plain list wrong.'}

    Write-Output '2. With the customer position: distance in km, nearest first'
    $near=Lockers '&latitude=12.0&longitude=105.0'
    if($near.list[0].name -ne "Distance $run B" -or $near.list[0].distanceKm -ne 0 -or $near.list[1].name -ne "Distance $run A"){throw 'Nearest-first order wrong.'}
    if($near.list[1].distanceKm -lt 1.1 -or $near.list[1].distanceKm -gt 1.12){throw "Distance wrong: $($near.list[1].distanceKm)"}
    $paged=Lockers '&latitude=12.0&longitude=105.0&page=2&take=1'
    if($paged.list.Count -ne 1 -or $paged.list[0].name -ne "Distance $run A"){throw 'Paging by distance wrong.'}

    Write-Output '3. Invalid position'
    Assert-Status 400 Get "/customer/lockers?latitude=95&longitude=105" $null $customer

    Write-Output 'PASS: no distance without a position, distance in km and nearest-first order (also across pages), invalid position rejected.'
}
finally {
    foreach($session in $sessions.Values){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{Write-Warning 'Smoke session logout failed.'}}
    foreach($id in $lockerIds){ Sql "UPDATE washgo.locker_slots SET is_deleted=true WHERE locker_id='$id'; UPDATE washgo.lockers SET status=3,is_deleted=true WHERE id='$id';" | Out-Null }
}
