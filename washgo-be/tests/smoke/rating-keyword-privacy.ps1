param(
    [string]$BaseUrl = 'http://localhost:5000',
    [string]$PostgresContainer = 'washgo_postgres',
    [string]$PostgresUser = 'postgres',
    [string]$Database = 'washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# LOCAL DEV ONLY. SQL fixtures isolate search visibility from order/payment workflows.
# Only newly cloned Completed orders/ratings are deleted; seed data is never edited.
$sessions = @{}
$userIds = @()
$orderIds = @()
$run = [Guid]::NewGuid().ToString('N')
$keyword = "ratingsearch$run"
$privateKeyword = "unreleased$run"
$otherMerchant = [Guid]::NewGuid()
$cases = @(
    @{name='pending'; hidden=$false; released=$false; deleted=$false; foreign=$false},
    @{name='released1'; hidden=$false; released=$true; deleted=$false; foreign=$false},
    @{name='released2'; hidden=$false; released=$true; deleted=$false; foreign=$false},
    @{name='hidden'; hidden=$true; released=$true; deleted=$false; foreign=$false},
    @{name='deleted'; hidden=$false; released=$true; deleted=$true; foreign=$false},
    @{name='foreign'; hidden=$false; released=$true; deleted=$false; foreign=$true},
    @{name='nullComment'; hidden=$false; released=$true; deleted=$false; foreign=$false}
)

function Assert-Search($Path, $Body, $Token, $ExpectedIds, $ExpectedTotal) {
    $result = Call-Api Post $Path $Body $Token
    $actual = @($result.list | ForEach-Object { "$($_.id)" } | Sort-Object)
    $expected = @($ExpectedIds | ForEach-Object { "$_" } | Sort-Object)
    if (!$result.succeeded -or $result.total -ne $ExpectedTotal -or
        ($actual -join ',') -ne ($expected -join ',')) {
        throw "Rating search mismatch: $Path (expected total $ExpectedTotal, got $($result.total))."
    }
    return $result
}

try {
    foreach ($role in @('Admin','Merchant','Customer')) {
        # Reuse the local Admin seed password hash without reading/exposing it.
        # Dedicated users avoid relying on a Merchant seed email or editing real accounts.
        $userId = [Guid]::NewGuid()
        $userIds += $userId
        $email = "rating-$($role.ToLower())-$run@example.com"
        Sql @"
INSERT INTO washgo.users
SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
 'id','$userId','role_id',r.id,'email','$email','full_name','Rating search fixture',
 'phone',NULL,'auth_provider',1,'provider_id',NULL,'is_active',true,'last_login_at',NULL,'avatar',NULL))).*
FROM washgo.users u CROSS JOIN washgo.roles r
WHERE u.email='admin@washgo.vn' AND r.name='$role';
"@ | Out-Null
        if ($role -eq 'Merchant') {
            Sql @"
INSERT INTO washgo.merchants
SELECT (jsonb_populate_record(NULL::washgo.merchants,to_jsonb(m)||jsonb_build_object(
 'user_id','$userId','business_name','Rating search fixture','tax_code',NULL,'status',1))).*
FROM washgo.merchants m LIMIT 1;
"@ | Out-Null
        }
        $sessions[$role] = (Call-Api Post '/auth/login' @{email=$email;password='Admin@123'} $null).data
    }
    $admin = $sessions.Admin.accessToken
    $merchant = $sessions.Merchant.accessToken
    $merchantId = [Guid]$sessions.Merchant.user.id
    if ((Sql 'SELECT count(*) FROM washgo.orders;') -eq '0') {
        throw 'Create one local development order before running this fixture test.'
    }
    foreach ($case in $cases) {
        $case.id = [Guid]::NewGuid()
        $case.orderId = [Guid]::NewGuid()
        $case.code = "RS$([Guid]::NewGuid().ToString('N'))"
        $orderIds += $case.orderId
        $owner = if ($case.foreign) { $otherMerchant } else { $merchantId }
        $hidden = if ($case.hidden) { 'true' } else { 'false' }
        $deleted = if ($case.deleted) { 'true' } else { 'false' }
        $moderated = if ($case.released) { 'now()' } else { 'NULL' }
        $comment = if ($case.name -eq 'nullComment') { 'NULL' }
            elseif ($case.name -eq 'pending') { "'$keyword $privateKeyword'" }
            else { "'$keyword'" }
        Sql @"
BEGIN;
INSERT INTO washgo.orders
SELECT (jsonb_populate_record(NULL::washgo.orders,to_jsonb(o)||jsonb_build_object(
 'id','$($case.orderId)','order_code','$($case.code)','merchant_order_code',NULL,
 'receipt_number',NULL,'status',12,'is_deleted',false))).*
FROM washgo.orders o LIMIT 1;
INSERT INTO washgo.order_ratings
 (id,order_id,order_code,customer_id,customer_name,merchant_id,merchant_name,
 overall_stars,comment,image_urls,video_urls,is_hidden,moderated_at,content_approved_at,is_deleted,created_on,modified_on)
SELECT '$($case.id)',id,order_code,customer_id,'Rating fixture','$owner','Rating fixture',
 4,$comment,ARRAY[]::text[],ARRAY[]::text[],$hidden,$moderated,$moderated,$deleted,
 extract(epoch from now())::bigint,extract(epoch from now())::bigint
FROM washgo.orders WHERE id='$($case.orderId)';
COMMIT;
"@ | Out-Null
    }
    $released = @($cases | Where-Object { $_.name -in @('released1','released2') } | ForEach-Object { $_.id })
    Assert-Search '/rating/partner/filter' @{keyword=$privateKeyword;take=100} $merchant @() 0 | Out-Null
    $pending = $cases | Where-Object name -eq 'pending'
    Assert-Search '/rating/filter' @{keyword=$privateKeyword} $admin @($pending.id) 1 | Out-Null
    $summary = (Call-Api Post '/rating/summary' @{keyword=$privateKeyword} $admin).data
    if ($summary.total -ne 1) { throw 'Admin summary lost access to unreviewed comments.' }

    $found = Assert-Search '/rating/partner/filter' @{keyword="  $($keyword.ToUpperInvariant())  ";take=100;merchantId="$otherMerchant";isHidden=$true} $merchant $released 2
    foreach ($item in $found.list) {
        if (!$item.contentReleased -or !$item.comment) { throw 'Keyword matched a withheld comment.' }
    }
    $page1 = Call-Api Post '/rating/partner/filter' @{keyword=$keyword;page=1;take=1} $merchant
    $page2 = Call-Api Post '/rating/partner/filter' @{keyword=$keyword;page=2;take=1} $merchant
    $page3 = Call-Api Post '/rating/partner/filter' @{keyword=$keyword;page=3;take=1} $merchant
    $pagedIds = @(@($page1.list) + @($page2.list) | ForEach-Object { "$($_.id)" } | Sort-Object)
    if ($page1.total -ne 2 -or $page2.total -ne 2 -or $page3.total -ne 2 -or
        @($page3.list).Count -ne 0 -or ($pagedIds -join ',') -ne (($released | Sort-Object) -join ',')) {
        throw 'Pagination leaked withheld rows or used a different count predicate.'
    }
    $withoutKeyword = Call-Api Post '/rating/partner/filter' @{take=100} $merchant
    $unreviewed = @($withoutKeyword.list | Where-Object { $_.id -eq $pending.id })
    if ($unreviewed.Count -ne 1 -or $unreviewed[0].comment -or $unreviewed[0].contentReleased) {
        throw 'Search fix incorrectly removed unreviewed stars or exposed their comment.'
    }
    $adminMatches = @($cases | Where-Object { !$_.deleted -and $_.name -ne 'nullComment' } | ForEach-Object { $_.id })
    Assert-Search '/rating/filter' @{keyword=$keyword;take=100} $admin $adminMatches 5 | Out-Null
    $approved = $cases | Where-Object name -eq 'released1'
    Assert-Search '/rating/partner/filter' @{keyword=$approved.code} $merchant @() 0 | Out-Null
    Assert-Search '/rating/filter' @{keyword=$approved.code} $admin @($approved.id) 1 | Out-Null
    Assert-Status 401 Post '/rating/partner/filter' @{keyword=$keyword} $null
    Assert-Status 403 Post '/rating/partner/filter' @{keyword=$keyword} $sessions.Customer.accessToken
    Write-Output 'PASS: unreviewed/hidden/deleted/foreign comments cannot leak through Merchant keyword, total or pagination; published comments and Admin search remain available.'
}
finally {
    if ($orderIds.Count) {
        $ids = ($orderIds | ForEach-Object { "'$_'" }) -join ','
        Sql "BEGIN; DELETE FROM washgo.order_ratings WHERE order_id IN ($ids); DELETE FROM washgo.orders WHERE id IN ($ids); COMMIT;" | Out-Null
    }
    foreach ($session in $sessions.Values) {
        if ($session.accessToken) {
            try { Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken | Out-Null }
            catch { Write-Warning 'Could not revoke a fixture login session.' }
        }
    }
    if ($userIds.Count) {
        $users = ($userIds | ForEach-Object { "'$_'" }) -join ','
        Sql "BEGIN; DELETE FROM washgo.refresh_tokens WHERE user_id IN ($users); DELETE FROM washgo.merchants WHERE user_id IN ($users); DELETE FROM washgo.users WHERE id IN ($users); COMMIT;" | Out-Null
    }
}
