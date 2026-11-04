param(
    [string]$BaseUrl = 'http://localhost:5000',
    [string]$PostgresContainer = 'washgo_postgres',
    [string]$PostgresUser = 'postgres',
    [string]$Database = 'washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# Private catalog fixtures only; never changes existing prices or services.
$prefix = 'CATALOG_' + [Guid]::NewGuid().ToString('N')
$ids = @(1..7 | ForEach-Object { [Guid]::NewGuid().ToString() })
$session = $null
function Catalog($Options = @{}) {
    $body = @{keyword=$prefix;page=1;take=100}
    foreach ($key in $Options.Keys) { $body[$key] = $Options[$key] }
    Call-Api Post '/customer/services/filter' $body $session.accessToken
}
function Assert-Values($Actual, $Expected) {
    if (@($Actual).Count -ne @($Expected).Count) { throw "Expected $Expected; got $Actual" }
    for ($index = 0; $index -lt @($Expected).Count; $index++) {
        if ($Actual[$index] -ne $Expected[$index]) { throw "Expected $Expected; got $Actual" }
    }
}
try {
    $session = (Call-Api Post '/auth/login' @{email='customer@washgo.vn';password='Customer@123'} $null).data
    $names = @('Zulu','Beta','Gamma','Alfa','Hidden','Pending','Deleted')
    $prices = @(10000,20000,20000,30000,15000,15000,15000)
    foreach ($i in 0..6) {
        Sql "INSERT INTO washgo.services
SELECT (jsonb_populate_record(NULL::washgo.services,to_jsonb(s)||jsonb_build_object(
'id','$($ids[$i])','code','${prefix}_$i','name','$($names[$i])','base_price',$($prices[$i]),
'is_deleted',false,'is_active',true,'status',2))).*
FROM washgo.services s LIMIT 1;" | Out-Null
    }
    Sql "UPDATE washgo.services SET is_active=false WHERE id='$($ids[4])';
UPDATE washgo.services SET status=1 WHERE id='$($ids[5])';
UPDATE washgo.services SET is_deleted=true WHERE id='$($ids[6])';" | Out-Null

    Write-Output '1. Default/name sorting and catalog visibility'
    $default = Catalog
    if ($default.total -ne 4) { throw 'Inactive/unapproved/deleted services leaked, or fixture setup failed.' }
    Assert-Values @($default.list.name) @('Alfa','Beta','Gamma','Zulu')
    Assert-Values @((Catalog @{sortBy=@(@{field='Name';isDescending=$true})}).list.name) @('Zulu','Gamma','Beta','Alfa')
    Write-Output '2. Price sorting, inclusive bounds, one-sided filters and combination'
    Assert-Values @((Catalog @{sortBy=@(@{field='UnitPrice';isDescending=$false})}).list.unitPrice) @(10000,20000,20000,30000)
    Assert-Values @((Catalog @{sortBy=@(@{field='UnitPrice';isDescending=$true})}).list.unitPrice) @(30000,20000,20000,10000)
    $range = Catalog @{sortBy=@(@{field='UnitPrice';isDescending=$true});minPrice=10000;maxPrice=20000}
    if ($range.total -ne 3) { throw 'Wrong filtered total.' }
    Assert-Values @($range.list.unitPrice) @(20000,20000,10000)
    if ((Catalog @{minPrice=20000;maxPrice=20000}).total -ne 2) { throw 'Equal bounds should include exact price.' }
    if ((Catalog @{minPrice=20000.01}).total -ne 1) { throw 'Decimal lower bound failed.' }
    if ((Catalog @{maxPrice=19999.99}).total -ne 1) { throw 'Decimal upper bound failed.' }
    if ((Catalog @{minPrice=0;maxPrice=0}).total -ne 0) { throw 'Empty range should return empty catalog.' }
    $type = Sql "SELECT category FROM washgo.services WHERE id='$($ids[0])';"
    if ((Catalog @{serviceType=[int]$type;minPrice=10000;maxPrice=20000}).total -ne 3) { throw 'Combined type/keyword/price filter failed.' }

    Write-Output '3. Stable pagination when prices tie'
    $first = Catalog @{sortBy=@(@{field='UnitPrice';isDescending=$false});page=1;take=2}
    $second = Catalog @{sortBy=@(@{field='UnitPrice';isDescending=$false});page=2;take=2}
    if ($first.total -ne 4 -or $second.total -ne 4 -or @($first.list).Count -ne 2 -or @($second.list).Count -ne 2) { throw 'Pagination totals incorrect.' }
    $pagedIds = @($first.list.id) + @($second.list.id)
    Assert-Values $pagedIds @((Catalog @{sortBy=@(@{field='UnitPrice';isDescending=$false})}).list.id)
    if (@($pagedIds | Select-Object -Unique).Count -ne 4) { throw 'Pagination duplicated/skipped a service.' }
    Write-Output '4. BaseFilterQuery defaults, multi-column sorting and validation'
    Assert-Values @((Catalog @{sortBy=$null}).list.id) @($default.list.id)
    $multi = Catalog @{
        sortBy=@(@{field='UnitPrice';isDescending=$false},@{field='Name';isDescending=$true})
    }
    Assert-Values @($multi.list.name) @('Zulu','Gamma','Beta','Alfa')
    $defaults = Call-Api Post '/customer/services/filter' @{keyword=$prefix} $session.accessToken
    if ($defaults.currentPage -ne 1 -or $defaults.pageSize -ne 20) { throw 'DTO must default to Customer page 1/take 20.' }
    foreach ($invalid in @(
        @{sortBy=@(@{field='Invalid'})},@{sortBy=@($null)},@{sortBy=@(@{field=''})},
        @{minPrice=-1},@{maxPrice=-1},@{minPrice=30000;maxPrice=10000}
    )) {
        Assert-Status 400 Post '/customer/services/filter' $invalid $session.accessToken
    }
    Assert-Status 401 Post '/customer/services/filter' @{} $null
    Assert-Status 404 Get '/customer/services' $null $session.accessToken
    $detail = (Call-Api Get "/customer/services/$($ids[0])" $null $session.accessToken).data
    if ($detail.id -ne $ids[0]) { throw 'Service detail route is broken.' }
    Write-Output 'PASS: POST service filter, removed GET list, retained GET detail, sorting, price bounds and pagination.'
} finally {
    $fixtureIds = ($ids | ForEach-Object { "'$_'" }) -join ','
    Sql "DELETE FROM washgo.services WHERE id IN ($fixtureIds);" | Out-Null
    if ($session.accessToken) {
        Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken | Out-Null
    }
}
