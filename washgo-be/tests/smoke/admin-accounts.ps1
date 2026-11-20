param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# LOCAL DEV ONLY: Admin account management. Creates fixture accounts (acct-smoke-<run>-*@example.com) and one fixture
# locker; removes them in finally. Covers identity uniqueness on non-deleted accounts, lock/unlock session effects,
# soft delete/re-register/restore, obligations, Admin protections (self, protected, re-authentication, last privileged
# Admin under concurrency), permissions, bulk with replay/lookup, activities and the Shipper page sharing the rules.
Add-Type -AssemblyName System.Net.Http
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$sessions=@{}; $lockerId=$null; $standardWash='b1111111-bbbb-1111-bbbb-111111111111'
$seedAdminId='a1111111-1111-1111-1111-111111111111'
# bcrypt('246810'): a fixed OTP for the email-change confirmation fixture.
$otpHash='$2b$10$s0tovPJVGZX.wjBxnq0j0.NhsUneTt5nT0hFgDiUjFWf2e1En1//q'

function Raw($Method,$Path,$Body,$Token,[hashtable]$Headers=@{}){
    $request=@{UseBasicParsing=$true;Method=$Method;Uri="$BaseUrl$Path";Headers=@{};TimeoutSec=30}
    if($Token){$request.Headers.Authorization="Bearer $Token"}
    foreach($k in $Headers.Keys){$request.Headers[$k]=$Headers[$k]}
    if($null -ne $Body){ $request.ContentType='application/json; charset=utf-8'; $request.Body=[Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Depth 10)) }
    try { $r=Invoke-WebRequest @request; return @{Status=[int]$r.StatusCode;Json=($r.Content | ConvertFrom-Json);Text=$r.Content} }
    catch {
        if($null -eq $_.Exception.Response){throw}
        $text=$_.ErrorDetails.Message
        if(!$text){ try { $text=(New-Object IO.StreamReader($_.Exception.Response.GetResponseStream())).ReadToEnd() } catch { } }
        $json=if($text){ try { $text | ConvertFrom-Json } catch { $null } } else { $null }
        return @{Status=[int]$_.Exception.Response.StatusCode;Json=$json;Text=$text}
    }
}
function Check($Condition,$Message){ if(!$Condition){throw $Message} }
function Expect($Response,$Status,$Code,$What){
    Check ($Response.Status -eq $Status) "$What`: expected HTTP $Status, got $($Response.Status) $($Response.Json.meta.code) $($Response.Json.message)"
    if($Code){ Check ($Response.Json.meta.code -eq $Code) "$What`: expected code $Code, got $($Response.Json.meta.code)" }
    $Response
}
function Op(){ [Guid]::NewGuid().ToString() }
function Email($Tag){ "acct-smoke-$run-$Tag@example.com" }
function Phone(){ '09' + (Get-Random -Minimum 10000000 -Maximum 99999999) }
function Account($Id){ (Expect (Raw Get "/admin/accounts/$Id" $null $admin) 200 $null "Get $Id").Json.data }
function Login($Email,$Password){ Raw Post '/auth/login' @{email=$Email;password=$Password} $null }
function Reauth(){ Expect (Raw Post '/auth/reauthenticate' @{password='Admin@123'} $admin) 200 $null 'Reauthenticate' | Out-Null }
function Lock($Id,$Rev,[bool]$Confirm=$false,$Token=$admin){ Raw Post "/admin/accounts/$Id/status" @{operationId=(Op);isActive=$false;reason='Smoke lock';expectedRevision=$Rev;confirm=$Confirm} $Token }
function Unlock($Id,$Rev){ Raw Post "/admin/accounts/$Id/status" @{operationId=(Op);isActive=$true;expectedRevision=$Rev} $admin }
function SoftDelete($Id,$Rev){ Raw Post "/admin/accounts/$Id/soft-delete" @{operationId=(Op);reason='Smoke delete';expectedRevision=$Rev} $admin }
function Customer-Body($Tag,[hashtable]$Extra=@{}){
    $b=@{operationId=(Op);role='Customer';fullName="Acct Smoke $Tag";email=(Email $Tag);phone=(Phone)}
    foreach($k in $Extra.Keys){$b[$k]=$Extra[$k]}; $b
}
function Send-Parallel($Requests){
    $client=[System.Net.Http.HttpClient]::new()
    try {
        $tasks=@(foreach($r in $Requests){
            $message=[System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::new($r.Method), "$BaseUrl$($r.Path)")
            $message.Headers.Authorization=[System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer',$r.Token)
            $message.Content=[System.Net.Http.StringContent]::new(($r.Body | ConvertTo-Json -Depth 10),[Text.Encoding]::UTF8,'application/json')
            $client.SendAsync($message)
        })
        [System.Threading.Tasks.Task]::WaitAll([System.Threading.Tasks.Task[]]$tasks)
        @(foreach($t in $tasks){ [int]$t.Result.StatusCode })
    } finally { $client.Dispose() }
}

try {
    foreach($role in @('Admin','Merchant','Customer')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken; $customer=$sessions.Customer.accessToken
    $adminId=$sessions.Admin.user.id
    $customerHash=Sql "SELECT password_hash FROM washgo.users WHERE email='customer@washgo.vn';"
    $adminHash=Sql "SELECT password_hash FROM washgo.users WHERE email='admin@washgo.vn';"

    Write-Output '1. Re-authentication and creation: normalized email, replay, unique among accounts not deleted, field errors, races'
    Expect (Raw Post '/auth/reauthenticate' @{password='wrong-Password1'} $admin) 400 'WrongPassword' 'Wrong re-authentication' | Out-Null
    Reauth
    $bodyA=Customer-Body 'a' @{email=("Acct-Smoke-$run-A@Example.COM")}
    $created=Expect (Raw Post '/admin/accounts' $bodyA $admin) 201 $null 'Create Customer A'
    $a=$created.Json.data
    Check ($a.email -ceq (Email 'a')) "Email not normalized: $($a.email)"
    Check ($a.role -eq 'Customer' -and $a.isActive -and !$a.isDeleted -and $null -eq $a.avatarUrl) 'Created Customer shape is wrong.'
    Check ($created.Text -notmatch 'passwordHash|providerId|"token"|temporaryPassword') 'Account DTO leaks secrets.'
    Check ((Sql "SELECT count(*) FROM washgo.customers WHERE user_id='$($a.id)';") -eq '1') 'Customer profile missing.'
    $replay=Raw Post '/admin/accounts' $bodyA $admin
    Check ($replay.Status -in 200,201 -and $replay.Json.data.id -eq $a.id) 'Create replay made another account.'
    $reused=$bodyA.Clone(); $reused.fullName='Other Name'
    Expect (Raw Post '/admin/accounts' $reused $admin) 409 'OperationIdReused' 'Reused operationId' | Out-Null
    $dup=Expect (Raw Post '/admin/accounts' (Customer-Body 'dup' @{email=("  ACCT-SMOKE-$run-a@example.com ")}) $admin) 409 'EmailAlreadyInUse' 'Duplicate email (case/space)'
    Check ($null -ne $dup.Json.errors.email) 'Duplicate email error is not on the email field.'
    $bad=Expect (Raw Post '/admin/accounts' @{operationId=(Op);role='Customer';fullName='x';email='not-an-email';phone='123'} $admin) 400 'ValidationFailed' 'Invalid create'
    Check ($bad.Json.errors.fullName -and $bad.Json.errors.email -and $bad.Json.errors.phone) 'Field errors missing.'
    Expect (Raw Post '/admin/accounts' @{operationId=(Op);role='Root';fullName='Root'} $admin) 400 'ValidationFailed' 'Unknown role' | Out-Null
    $raceEmail=Email 'race'
    $codes=Send-Parallel @(
        @{Method='POST';Path='/admin/accounts';Token=$admin;Body=(Customer-Body 'race' @{email=$raceEmail})},
        @{Method='POST';Path='/admin/accounts';Token=$admin;Body=(Customer-Body 'race' @{email=$raceEmail.ToUpper()})})
    Check ((@($codes | Where-Object {$_ -eq 201}).Count -eq 1) -and (@($codes | Where-Object {$_ -eq 409}).Count -eq 1)) "Concurrent same-email creates: $($codes -join ',')"
    Check ((Sql "SELECT count(*) FROM washgo.users WHERE lower(email)='$raceEmail' AND NOT is_deleted;") -eq '1') 'Race created two accounts.'
    $b=(Expect (Raw Post '/admin/accounts' @{operationId=(Op);role='Admin';fullName="Acct Smoke Admin $run";email=(Email 'b');phone=(Phone)} $admin) 201 $null 'Create Admin B').Json.data
    Check (@($b.permissions).Count -eq 0) 'A new Admin got grants by default.'
    $d=(Expect (Raw Post '/admin/accounts' (Customer-Body 'd') $admin) 201 $null 'Create Customer D').Json.data

    Write-Output '2. List, filters, sorting, validation and summary'
    $page=(Expect (Raw Get "/admin/accounts?keyword=acct-smoke-$run&take=2&sortBy=fullName" $null $admin) 200 $null 'List').Json.data
    Check ($page.total -eq 4 -and @($page.items).Count -eq 2 -and $page.hasMore) "List paging wrong: total $($page.total)"
    $all=@((Call-Api Get "/admin/accounts?keyword=acct-smoke-$run&take=100&sortBy=fullName&sortDirection=asc" $null $admin).data.items)
    # The database collation decides letter case order (C collation in CI, locale collation elsewhere): accept either.
    $names=[string[]]@($all | ForEach-Object fullName); $ordinal=[string[]]$names.Clone(); [Array]::Sort($ordinal,[StringComparer]::Ordinal)
    $cultural=[string[]]@($names | Sort-Object)
    Check (($names -join '|') -ceq ($ordinal -join '|') -or ($names -join '|') -ceq ($cultural -join '|')) "fullName sort is not applied: $($names -join ', ')"
    $rowA=$all | Where-Object id -eq $a.id
    Check ($rowA.capabilities.canLock -and $rowA.capabilities.canDelete -and !$rowA.capabilities.canUnlock) 'Customer capabilities wrong.'
    $rowB=$all | Where-Object id -eq $b.id
    Check ($rowB.capabilities.requiresReauthentication -and $rowB.capabilities.canManagePermissions) 'Admin capabilities wrong.'
    $self=(Call-Api Get "/admin/accounts?keyword=admin@washgo.vn" $null $admin).data.items | Where-Object id -eq $adminId
    Check (!$self.capabilities.canLock -and ($self.capabilities.blocked | Where-Object { $_.action -eq 'Lock' -and $_.code -eq 'CannotModifySelf' })) 'Self lock is offered.'
    Expect (Raw Get '/admin/accounts?take=101' $null $admin) 400 'ValidationFailed' 'take 101' | Out-Null
    Expect (Raw Get '/admin/accounts?sortBy=email' $null $admin) 400 'ValidationFailed' 'sortBy email' | Out-Null
    Expect (Raw Get '/admin/accounts?role=Boss' $null $admin) 400 'ValidationFailed' 'role Boss' | Out-Null
    Expect (Raw Get '/admin/accounts?sortBy=lastLoginAt&role=Customer&isActive=true' $null $admin) 200 $null 'lastLoginAt sort' | Out-Null
    $sum=(Call-Api Get '/admin/accounts/summary' $null $admin).data
    $byRole=0; foreach($p in $sum.byRole.PSObject.Properties){ $byRole+=$p.Value.total }
    Check ($byRole -eq $sum.total -and $sum.active + $sum.locked -eq $sum.total -and $null -ne $sum.deleted) 'Summary totals are inconsistent.'

    Write-Output '3. Lock/unlock: reason, revision, open-order confirmation, sessions revoked, no revival'
    Sql "UPDATE washgo.users SET password_hash='$customerHash' WHERE id='$($a.id)';" | Out-Null
    $loginA=(Expect (Login (Email 'a') 'Customer@123') 200 $null 'Login A').Json.data
    $a=Account $a.id; $revBefore=$a.revision
    Expect (Login (Email 'a') 'Customer@123') 200 $null 'Login A again' | Out-Null
    Check ((Account $a.id).revision -eq $revBefore) 'A sign-in changed the account revision.'
    Expect (Raw Post "/admin/accounts/$($a.id)/status" @{operationId=(Op);isActive=$false;expectedRevision=$a.revision} $admin) 400 'ValidationFailed' 'Lock without reason' | Out-Null
    Expect (Raw Post "/admin/accounts/$($a.id)/status" @{operationId=(Op);isActive=$false;reason='x'} $admin) 400 'ValidationFailed' 'Lock without revision' | Out-Null
    Expect (Lock $a.id ($a.revision + 5)) 409 'RevisionConflict' 'Stale lock' | Out-Null
    $locker=(Call-Api Post '/locker/' @{merchantIds=@("$($sessions.Merchant.user.id)");name="Acct Locker $run";address='Private development fixture';city='Test';latitude=10.77;longitude=106.69;boxCount=2} $admin).data
    $lockerId=[Guid]$locker.id
    $slots=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    $order=(Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$slots.availableSlots[0].id;paymentMethod=2;items=@(@{serviceId=$standardWash;quantity=1})} $customer).data
    Sql "UPDATE washgo.orders SET customer_id='$($a.id)' WHERE id='$($order.id)';" | Out-Null
    $confirm=Expect (Lock $a.id $a.revision) 409 'ConfirmationRequired' 'Lock with open order'
    Check ($confirm.Json.meta.openOrders -ge 1) 'Open order count missing.'
    $locked=(Expect (Lock $a.id $a.revision $true) 200 $null 'Confirmed lock').Json.data
    Check (!$locked.isActive -and $locked.statusReason -eq 'Smoke lock' -and $locked.statusChangedBy.id -eq $adminId -and $locked.revision -gt $a.revision) 'Lock metadata wrong.'
    Check ((Raw Get '/auth/me' $null $loginA.accessToken).Status -eq 401) 'Locked account access token still works.'
    Check ((Raw Post '/auth/refresh-token' @{refreshToken=$loginA.refreshToken} $null).Status -eq 400) 'Locked account refreshed.'
    Check ((Login (Email 'a') 'Customer@123').Status -eq 400) 'Locked account signed in.'
    $again=Expect (Lock $a.id $locked.revision $true) 200 $null 'Lock again'
    Check ($again.Json.meta.outcome -eq 'Unchanged' -and $again.Json.data.revision -eq $locked.revision) 'Repeated lock changed the account.'
    $open=(Expect (Unlock $a.id $locked.revision) 200 $null 'Unlock').Json.data
    Check ($open.isActive) 'Unlock did not open the account.'
    Check ((Raw Post '/auth/refresh-token' @{refreshToken=$loginA.refreshToken} $null).Status -eq 400) 'Unlock revived an old session.'
    Expect (Login (Email 'a') 'Customer@123') 200 $null 'Login after unlock' | Out-Null

    Write-Output '4. Admin protections: self, protected account, re-authentication, permissions'
    $me=Account $adminId
    Expect (Lock $adminId $me.revision) 409 'CannotModifySelf' 'Self lock' | Out-Null
    Check ((Sql "SELECT count(*) FROM washgo.audit_logs WHERE module='Account' AND resource_id='$adminId' AND error_code='CannotModifySelf';") -ge 1) 'Refused self lock left no audit trace.'
    Sql "UPDATE washgo.users SET is_protected=true WHERE id='$($b.id)';" | Out-Null
    $b=Account $b.id
    Expect (Lock $b.id $b.revision) 409 'ProtectedAccount' 'Protected Admin lock' | Out-Null
    Sql "UPDATE washgo.users SET is_protected=false WHERE id='$($b.id)';" | Out-Null
    $b=Account $b.id
    Sql "UPDATE washgo.refresh_tokens SET reauthenticated_at=now() - interval '1 hour' WHERE user_id='$adminId' AND NOT is_revoked;" | Out-Null
    Expect (Lock $b.id $b.revision) 403 'ReauthenticationRequired' 'Admin lock without re-authentication' | Out-Null
    Reauth
    Expect (Raw Put "/admin/accounts/$($b.id)/permissions" @{operationId=(Op);permissions=@('Accounts.Everything');reason='x';expectedRevision=$b.revision} $admin) 400 'ValidationFailed' 'Unknown permission' | Out-Null
    $b=(Expect (Raw Put "/admin/accounts/$($b.id)/permissions" @{operationId=(Op);permissions=@('Accounts.ManageAdmins');reason='Smoke grant';expectedRevision=$b.revision} $admin) 200 $null 'Grant ManageAdmins').Json.data
    Check (@($b.permissions) -contains 'Accounts.ManageAdmins') 'Grant not stored.'
    Expect (Raw Put "/admin/accounts/$adminId/permissions" @{operationId=(Op);permissions=@();reason='x';expectedRevision=$me.revision} $admin) 409 'CannotModifySelf' 'Self permission change' | Out-Null

    Write-Output '5. Last privileged Admin: two privileged Admins locking each other at once cannot both succeed'
    Sql "UPDATE washgo.users SET password_hash='$adminHash' WHERE id='$($b.id)';" | Out-Null
    $sessions.B=(Expect (Login (Email 'b') 'Admin@123') 200 $null 'Login Admin B').Json.data
    Expect (Raw Post '/auth/reauthenticate' @{password='Admin@123'} $sessions.B.accessToken) 200 $null 'Reauthenticate B' | Out-Null
    $me=Account $adminId
    $race=Send-Parallel @(
        @{Method='POST';Path="/admin/accounts/$($b.id)/status";Token=$admin;Body=@{operationId=(Op);isActive=$false;reason='Race';expectedRevision=$b.revision}},
        @{Method='POST';Path="/admin/accounts/$adminId/status";Token=$sessions.B.accessToken;Body=@{operationId=(Op);isActive=$false;reason='Race';expectedRevision=$me.revision}})
    Check (@($race | Where-Object {$_ -eq 200}).Count -le 1) "Both privileged Admins were locked: $($race -join ',')"
    $holders=Sql "SELECT count(*) FROM washgo.user_permissions p JOIN washgo.users u ON u.id=p.user_id WHERE p.permission='Accounts.ManageAdmins' AND u.is_active AND NOT u.is_deleted;"
    Check ([int]$holders -ge 1) 'No active Admin can manage Admins after the race.'
    # Restore the seed Admin for the rest of the run (the race may have locked it on purpose).
    Sql "UPDATE washgo.users SET is_active=true, status_reason=NULL WHERE id='$adminId';" | Out-Null
    $sessions.Admin=(Call-Api Post '/auth/login' @{email='admin@washgo.vn';password='Admin@123'} $null).data; $admin=$sessions.Admin.accessToken
    Reauth

    Write-Output '6. Edit: field rules, If-Match, email only through verification'
    $a=Account $a.id
    $edited=(Expect (Raw Patch "/admin/accounts/$($a.id)" @{operationId=(Op);fullName="Acct Smoke A Edited";phone=(Phone)} $admin @{'If-Match'="`"$($a.revision)`""}) 200 $null 'Edit with If-Match').Json.data
    Check ($edited.fullName -eq 'Acct Smoke A Edited' -and $edited.revision -gt $a.revision) 'Edit not applied.'
    Expect (Raw Patch "/admin/accounts/$($a.id)" @{operationId=(Op);email=(Email 'other');expectedRevision=$edited.revision} $admin) 400 'UseEmailChangeFlow' 'Email in PATCH' | Out-Null
    $avatar=Expect (Raw Patch "/admin/accounts/$($a.id)" @{operationId=(Op);avatarUrl='https://evil.example/x.png';expectedRevision=$edited.revision} $admin) 400 'ValidationFailed' 'Foreign avatar URL'
    Check ($null -ne $avatar.Json.errors.avatarUrl) 'Avatar error not on its field.'
    Expect (Raw Post "/admin/accounts/$($a.id)/email-change" @{newEmail=(Email 'd');expectedRevision=$edited.revision} $admin) 409 'EmailAlreadyInUse' 'Email change to a used email' | Out-Null
    $newEmail=Email 'a2'
    Sql "INSERT INTO washgo.otp_codes(id,user_id,email,otp_code,purpose,expiry_date,is_used,attempt_count,max_attempts,created_at) VALUES (gen_random_uuid(),'$($a.id)','$newEmail','$otpHash',3,now()+interval '10 minutes',false,0,5,now());" | Out-Null
    Expect (Raw Post "/admin/accounts/$($a.id)/email-change/confirm" @{operationId=(Op);newEmail=$newEmail;code='111111';expectedRevision=$edited.revision} $admin) 400 'ValidationFailed' 'Wrong email-change code' | Out-Null
    $changed=(Expect (Raw Post "/admin/accounts/$($a.id)/email-change/confirm" @{operationId=(Op);newEmail=$newEmail;code='246810';expectedRevision=$edited.revision} $admin) 200 $null 'Confirm email change').Json.data
    Check ($changed.email -eq $newEmail) 'Email not changed.'

    Write-Output '7. Soft delete: obligations block, server time, no hard delete, re-register with a new id, restore conflict'
    $blocked=Expect (SoftDelete $a.id $changed.revision) 409 'AccountHasOpenObligations' 'Delete with open order'
    Check (@($blocked.Json.meta.blockers) -contains 'OpenOrders') 'Blockers do not name the open order.'
    Sql "UPDATE washgo.orders SET customer_id='$($sessions.Customer.user.id)' WHERE id='$($order.id)';" | Out-Null
    $deleted=(Expect (SoftDelete $a.id $changed.revision) 200 $null 'Soft delete A').Json.data
    Check ($deleted.isDeleted -and !$deleted.isActive -and $deleted.deletedAt -and $deleted.deletedBy.id -eq $adminId) 'Soft delete fields wrong.'
    $dbDeleted=Sql "SELECT count(*) || '|' || to_char(deleted_at,'YYYY-MM-DD HH24:MI:SS.US') FROM washgo.users WHERE id='$($a.id)' GROUP BY deleted_at;"
    Check ($dbDeleted -like '1|*') 'Account row was removed (hard delete).'
    $twice=Expect (SoftDelete $a.id $deleted.revision) 200 $null 'Delete again'
    Check ($twice.Json.meta.outcome -eq 'Unchanged' -and (Sql "SELECT to_char(deleted_at,'YYYY-MM-DD HH24:MI:SS.US') FROM washgo.users WHERE id='$($a.id)';") -eq $dbDeleted.Split('|')[1]) 'Repeated delete rewrote deletedAt.'
    Check ((Login $newEmail 'Customer@123').Status -eq 400) 'Deleted account signed in.'
    Check (@((Call-Api Get "/admin/accounts?keyword=acct-smoke-$run&take=100" $null $admin).data.items | Where-Object id -eq $a.id).Count -eq 0) 'Deleted account listed by default.'
    Check (@((Call-Api Get "/admin/accounts?keyword=acct-smoke-$run&take=100&deleted=Only&sortBy=deletedAt" $null $admin).data.items | Where-Object id -eq $a.id).Count -eq 1) 'deleted=Only misses the account.'
    Expect (Lock $a.id $deleted.revision) 409 'AccountAlreadyDeleted' 'Lock deleted account' | Out-Null
    $c=(Expect (Raw Post '/admin/accounts' (Customer-Body 'c' @{email=$newEmail.ToUpper()}) $admin) 201 $null 'Re-register deleted email').Json.data
    Check ($c.id -ne $a.id) 'Re-registration reused the deleted account id.'
    Expect (Raw Post "/admin/accounts/$($a.id)/restore" @{operationId=(Op);expectedRevision=$deleted.revision} $admin) 409 'IdentityAlreadyInUse' 'Restore over a new account' | Out-Null
    Expect (SoftDelete $c.id $c.revision) 200 $null 'Delete C' | Out-Null
    $restored=(Expect (Raw Post "/admin/accounts/$($a.id)/restore" @{operationId=(Op);reason='Smoke restore';expectedRevision=$deleted.revision} $admin) 200 $null 'Restore A').Json.data
    Check (!$restored.isDeleted -and !$restored.isActive -and $null -eq $restored.deletedAt) 'Restore must leave the account locked and not deleted.'

    Write-Output '8. Bulk: preview, partial success, replay, key reuse, lookup'
    $d=Account $d.id
    $preview=(Expect (Raw Post '/admin/accounts/bulk/preview' @{action='Lock';ids=@($d.id,$adminId,[Guid]::NewGuid().ToString())} $admin) 200 $null 'Bulk preview').Json.data
    $pd=$preview.items | Where-Object id -eq $d.id; $ps=$preview.items | Where-Object id -eq $adminId
    Check ($pd.canApply -and !$ps.canApply -and (@($ps.reasonCodes) -contains 'CannotModifySelf') -and $preview.blocked -eq 2) 'Preview verdicts wrong.'
    $bulk=@{operationId=(Op);action='Lock';reason='Smoke bulk';items=@(@{id=$d.id;expectedRevision=$d.revision},@{id=$restored.id;expectedRevision=($restored.revision + 9)})}
    $result=(Expect (Raw Post '/admin/accounts/bulk' $bulk $admin) 200 $null 'Bulk lock').Json.data
    $rd=$result.items | Where-Object id -eq $d.id; $ra=$result.items | Where-Object id -eq $restored.id
    Check ($rd.outcome -eq 'Changed' -and $ra.outcome -eq 'Failed' -and $ra.reasonCode -eq 'RevisionConflict' -and $result.totals.succeeded -eq 1 -and $result.totals.failed -eq 1) 'Bulk partial result wrong.'
    $replayBulk=(Expect (Raw Post '/admin/accounts/bulk' $bulk $admin) 200 $null 'Bulk replay').Json.data
    Check ((($replayBulk.items | ConvertTo-Json -Depth 6 -Compress) -eq ($result.items | ConvertTo-Json -Depth 6 -Compress))) 'Bulk replay changed results.'
    $other=$bulk.Clone(); $other.reason='Different'
    Expect (Raw Post '/admin/accounts/bulk' $other $admin) 409 'OperationIdReused' 'Bulk key reuse' | Out-Null
    $lookup=(Expect (Raw Get "/admin/accounts/operations/$($bulk.operationId)" $null $admin) 200 $null 'Operation lookup').Json.data
    Check ($lookup.state -eq 'Completed') 'Operation lookup state wrong.'
    $many=@(1..101 | ForEach-Object { @{id=[Guid]::NewGuid().ToString();expectedRevision=0} })
    Check ((Raw Post '/admin/accounts/bulk' @{operationId=(Op);action='Lock';reason='x';items=$many} $admin).Status -eq 400) '101 items accepted.'

    Write-Output '9. Activities and Shipper page sharing the account rules'
    $acts=(Call-Api Get "/admin/accounts/$($a.id)/activities?take=50" $null $admin)
    $actions=@($acts.list | ForEach-Object action)
    foreach($expected in @('Create','Lock','Unlock','SoftDelete','Restore','EmailChange')){ Check ($actions -contains $expected) "Activity $expected missing." }
    $shipperBody=@{operationId=(Op);role='Shipper';shipper=@{merchantId="$($sessions.Merchant.user.id)";fullName="Acct Smoke Shipper";phone=(Phone);email=(Email 's');vehicleType=1;vehiclePlate='59X1-99999'}}
    $s=(Expect (Raw Post '/admin/accounts' $shipperBody $admin) 201 $null 'Create Shipper via account page').Json.data
    Check ($s.role -eq 'Shipper' -and $s.partner.shipperId) 'Shipper account has no Shipper profile.'
    Call-Api Delete "/shipper/$($s.partner.shipperId)?reason=Smoke" $null $admin | Out-Null
    Check ((Sql "SELECT is_deleted::text || is_active::text FROM washgo.users WHERE id='$($s.id)';") -eq 'truefalse') 'Shipper page delete did not soft-delete the login account.'
    Expect (Raw Post '/admin/accounts' (Customer-Body 's2' @{email=(Email 's')}) $admin) 201 $null 'Reuse deleted Shipper email' | Out-Null

    Write-Output 'PASS: re-authentication, normalized unique email among non-deleted accounts (replay, races, field errors), list/sort/summary, lock/unlock session effects and confirmation, Admin self/protected/re-authentication/last-privileged protections, permissions, edit/If-Match/email verification, soft delete with obligations/no hard delete/re-register/restore conflict, bulk partial/replay/lookup, activities and Shipper page soft delete.'
}
finally {
    foreach($session in $sessions.Values){ try { Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken | Out-Null } catch { } }
    $fixtures="SELECT id FROM washgo.users WHERE email LIKE 'acct-smoke-$run-%'"
    # Cleanup must not hide the original failure.
    try { Sql @"
BEGIN;
UPDATE washgo.users SET is_active=true WHERE id='$seedAdminId';
UPDATE washgo.orders SET customer_id='$($sessions.Customer.user.id)' WHERE customer_id IN ($fixtures);
$(if($lockerId){"UPDATE washgo.orders SET status=99 WHERE locker_id='$lockerId' AND status NOT IN(12,99);
UPDATE washgo.locker_slots SET status=1,is_deleted=true WHERE locker_id='$lockerId';
UPDATE washgo.lockers SET available_slots=0,status=3,is_deleted=true WHERE id='$lockerId';"})
DELETE FROM washgo.notification_deliveries WHERE user_id IN ($fixtures);
DELETE FROM washgo.notifications WHERE user_id IN ($fixtures);
DELETE FROM washgo.refresh_tokens WHERE user_id IN ($fixtures);
DELETE FROM washgo.otp_codes WHERE user_id IN ($fixtures) OR email LIKE 'acct-smoke-$run-%';
DELETE FROM washgo.user_permissions WHERE user_id IN ($fixtures);
DELETE FROM washgo.shipper_audits WHERE shipper_id IN (SELECT id FROM washgo.shippers WHERE user_id IN ($fixtures));
DELETE FROM washgo.shippers WHERE user_id IN ($fixtures);
DELETE FROM washgo.customers WHERE user_id IN ($fixtures);
DELETE FROM washgo.users WHERE id IN ($fixtures);
COMMIT;
"@ | Out-Null } catch { Write-Warning "Account smoke cleanup failed: $($_.Exception.Message)" }
}
