param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$MailpitUrl='http://localhost:8025',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly
. "$PSScriptRoot/shipper-fixtures.ps1"
Add-Type -AssemblyName System.Net.Http

# LOCAL DEV ONLY: Merchant service catalog detail/images/summary/sort, proposal images, versions, idempotency, concurrency.
$sessions=@{}; $teamId=$null; $serviceIds=@()
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$http=New-Object System.Net.Http.HttpClient
function New-Request($Method,$Path,$Body,$Token){
    $req=New-Object System.Net.Http.HttpRequestMessage ([System.Net.Http.HttpMethod]::new($Method)),"$BaseUrl$Path"
    $req.Headers.Authorization=New-Object System.Net.Http.Headers.AuthenticationHeaderValue 'Bearer',$Token
    if($null -ne $Body){ $req.Content=New-Object System.Net.Http.StringContent (($Body|ConvertTo-Json -Depth 10),[Text.Encoding]::UTF8,'application/json') }
    $req
}
function Read-Response($R){ $t=$R.Content.ReadAsStringAsync().Result; @{ Status=[int]$R.StatusCode; Json=$(if($t){$t|ConvertFrom-Json}else{$null}) } }
function Send($Method,$Path,$Body,$Token){ Read-Response ($http.SendAsync((New-Request $Method $Path $Body $Token)).Result) }
function Proposal($Name,$Extra){ $b=@{name=$Name;description='Dich vu thu';unitPrice=100000;estimatedDurationHours=24;serviceType=1;unit='kg';reason='Thu'}; foreach($k in $Extra.Keys){$b[$k]=$Extra[$k]}; $b }
function Approve($Id){ $sid=(Call-Api Post "/service-proposal/$Id/approve" $null $admin).data.serviceId; $script:serviceIds+=$sid; $sid }

try {
    foreach($role in @('Admin','Merchant')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$seed=$sessions.Merchant.accessToken
    $sessions.Team=New-FixtureMerchant $run; $team=$sessions.Team.accessToken; $teamId=$sessions.Team.user.id
    $img1=Upload-TestImage $team; $img2=Upload-TestImage $team; $seedImg=Upload-TestImage $seed

    Write-Output '1. Validation codes and idempotent create'
    $bad=Send Post '/merchant-service/proposals' (Proposal "Svc $run x" @{unitPrice=1000.123}) $team
    if($bad.Status -ne 400 -or $bad.Json.meta.code -ne 'InvalidPrice'){throw 'Price precision not checked.'}
    if((Send Post '/merchant-service/proposals' (Proposal "Svc $run x" @{pricePerKg=200000000}) $team).Status -ne 400){throw 'Optional price limit not checked.'}
    $k=[Guid]::NewGuid()
    $a=Send Post '/merchant-service/proposals' (Proposal "Svc $run A" @{unitPrice=50000;imageUrls=@($img1,$img2);clientRequestId=$k}) $team
    $a2=Send Post '/merchant-service/proposals' (Proposal "Svc $run A" @{unitPrice=50000;imageUrls=@($img1,$img2);clientRequestId=$k}) $team
    if($a.Status -ne 201 -or $a2.Status -ne 200 -or !$a2.Json.meta.replayed -or $a2.Json.data.id -ne $a.Json.data.id){throw "Replay wrong: $($a.Status)/$($a2.Status)"}
    $notes=@((Call-Api Get '/notification?take=100' $null $admin).list | Where-Object { $_.linkId -eq $a.Json.data.id })
    if($notes.Count -ne 1){throw "Admin notified $($notes.Count) times."}

    Write-Output '2. Two concurrent proposals with the same new name: one wins'
    $t1=$http.SendAsync((New-Request Post '/merchant-service/proposals' (Proposal "Svc $run B" @{unitPrice=90000}) $team))
    $t2=$http.SendAsync((New-Request Post '/merchant-service/proposals' (Proposal "Svc $run B" @{unitPrice=90000}) $seed))
    [void][System.Threading.Tasks.Task]::WaitAll(@($t1,$t2))
    $r1=Read-Response $t1.Result; $r2=Read-Response $t2.Result
    if((@($r1.Status,$r2.Status) | Sort-Object) -join ',' -ne '201,400' -or @($r1,$r2 | Where-Object { $_.Status -eq 400 })[0].Json.meta.code -ne 'DuplicateName'){throw "Concurrent names: $($r1.Status)/$($r2.Status)"}
    $bId=@($r1,$r2 | Where-Object { $_.Status -eq 201 })[0].Json.data.id

    Write-Output '3. Concurrent approvals: one result'
    $t1=$http.SendAsync((New-Request Post "/service-proposal/$($a.Json.data.id)/approve" $null $admin))
    $t2=$http.SendAsync((New-Request Post "/service-proposal/$($a.Json.data.id)/approve" $null $admin))
    [void][System.Threading.Tasks.Task]::WaitAll(@($t1,$t2))
    $x1=Read-Response $t1.Result; $x2=Read-Response $t2.Result
    if((@($x1.Status,$x2.Status) | Sort-Object) -join ',' -ne '200,400'){throw "Concurrent approve: $($x1.Status)/$($x2.Status)"}
    $sA=@($x1,$x2 | Where-Object { $_.Status -eq 200 })[0].Json.data.serviceId; $serviceIds+=$sA
    $sB=Approve $bId

    Write-Output '4. Catalog detail, images, version, sort, summary'
    $d=(Call-Api Get "/merchant-service/$sA" $null $seed).data
    if((@($d.imageUrls) -join ',') -ne "$img1,$img2" -or $d.thumbnailUrl -ne $img1 -or !$d.version -or !$d.canProposeUpdate -or $d.hasPendingProposal){throw 'Detail wrong.'}
    Assert-Status 404 Get "/merchant-service/$([Guid]::NewGuid())" $null $seed
    $desc=(Call-Api Post '/merchant-service/filter' @{keyword="Svc $run";page=1;take=10;sortBy=@(@{field='UnitPrice';isDescending=$true})} $seed).list
    if((($desc | ForEach-Object { $_.id }) -join ',') -ne "$sB,$sA"){throw 'Price sort wrong.'}
    Assert-Status 400 Post '/merchant-service/filter' @{page=1;take=10;sortBy=@(@{field='Code';isDescending=$true})} $seed
    $sum=(Call-Api Get '/merchant-service/summary' $null $team).data
    $catalog=(Call-Api Post '/merchant-service/filter' @{page=1;take=1} $team).total
    # Proposal B was raced by both Merchants, so the Team may own one or two approved proposals.
    $approved=(Call-Api Post '/service-proposal/filter' @{status=2;page=1;take=1} $team).total
    if($sum.totalServices -ne $catalog -or $sum.activeServices + $sum.inactiveServices -ne $sum.totalServices -or $sum.approvedProposals -ne $approved -or $approved -lt 1 -or $sum.pendingProposals -ne 0){throw "Summary wrong: $($sum | ConvertTo-Json -Compress)"}

    Write-Output '5. Edit proposals: keep images by null, other Merchant images allowed only if already on the service, [] clears'
    $keep=(Call-Api Post "/merchant-service/$sA/proposals" (Proposal "Svc $run A" @{unitPrice=60000;expectedVersion=$d.version}) $seed).data
    if((@($keep.proposed.imageUrls) -join ',') -ne "$img1,$img2"){throw 'Null did not keep images.'}
    $teamView=(Call-Api Get "/merchant-service/$sA" $null $team).data
    $seedView=(Call-Api Get "/merchant-service/$sA" $null $seed).data
    if(!$teamView.hasPendingProposal -or $teamView.canProposeUpdate -or $teamView.proposeBlockedCode -ne 'PendingProposal' -or $teamView.myPendingProposalId -or $seedView.myPendingProposalId -ne $keep.id){throw 'Pending flags wrong.'}
    $pend=Send Post "/merchant-service/$sA/proposals" (Proposal "Svc $run A" @{}) $team
    if($pend.Status -ne 400 -or $pend.Json.meta.code -ne 'PendingProposal'){throw 'Pending code missing.'}
    Call-Api Post "/service-proposal/$($keep.id)/reject" @{reason='Thu'} $admin | Out-Null
    $mix=(Call-Api Post "/merchant-service/$sA/proposals" (Proposal "Svc $run A" @{imageUrls=@($img2,$seedImg)}) $seed).data
    if((@($mix.proposed.imageUrls) -join ',') -ne "$img2,$seedImg"){throw 'Existing + own new image not accepted.'}
    Call-Api Post "/service-proposal/$($mix.id)/reject" @{reason='Thu'} $admin | Out-Null
    Assert-Status 400 Post "/merchant-service/$sA/proposals" (Proposal "Svc $run A" @{imageUrls=@($seedImg)}) $team
    $clear=(Call-Api Post "/merchant-service/$sA/proposals" (Proposal "Svc $run A" @{imageUrls=@()}) $seed).data
    if(@($clear.proposed.imageUrls).Count -ne 0){throw '[] did not clear images.'}

    Write-Output '6. Stale version on submit and on approval'
    Sql "UPDATE washgo.services SET description='Admin vua sua $run' WHERE id='$sA';" | Out-Null
    $st=Send Post "/service-proposal/$($clear.id)/approve" $null $admin
    if($st.Status -ne 409 -or $st.Json.meta.code -ne 'StaleVersion'){throw "Stale approve not blocked: $($st.Status)"}
    Call-Api Post "/service-proposal/$($clear.id)/reject" @{reason='Ban cu'} $admin | Out-Null
    $old=Send Post "/merchant-service/$sA/proposals" (Proposal "Svc $run A" @{expectedVersion=$d.version}) $seed
    if($old.Status -ne 409 -or $old.Json.meta.code -ne 'StaleVersion'){throw "Stale submit not blocked: $($old.Status)"}
    if((Call-Api Get "/merchant-service/$sA" $null $seed).data.version -eq $d.version){throw 'Version did not change.'}

    Write-Output '7. Proposal filter by keyword and serviceId'
    $byService=Call-Api Post '/service-proposal/filter' @{serviceId="$sA";page=1;take=20} $seed
    if($byService.total -ne 3){throw "serviceId filter wrong: $($byService.total)"}
    if((Call-Api Post '/service-proposal/filter' @{keyword="svc $run b";page=1;take=20} $admin).total -ne 1){throw 'Keyword filter wrong.'}

    Write-Output 'PASS: price limits/precision with InvalidPrice code; clientRequestId replay without a second Admin notification; concurrent same-name proposals 201/400 DuplicateName; concurrent approvals 200/400; Merchant catalog detail (404 unknown) with ordered images, thumbnail, version and proposal flags; whitelisted price sort with stable order, other sort fields 400; summary of catalog and own proposals; edit images null keeps / [] clears / existing service images kept / foreign new images rejected; PendingProposal code; stale version blocked at submit (expectedVersion) and at approval (409 StaleVersion); proposal filter by serviceId and keyword.'
}
finally {
    foreach($session in $sessions.Values){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{Write-Warning 'Smoke session logout failed.'}}
    foreach($id in $serviceIds){ if($id){ Sql "UPDATE washgo.services SET is_deleted=true, is_active=false WHERE id='$id'; UPDATE washgo.service_change_requests SET status=3 WHERE service_id='$id' AND status=1;" | Out-Null } }
    Sql "UPDATE washgo.service_change_requests SET status=3, rejection_reason='smoke cleanup' WHERE status=1 AND proposed_name LIKE 'Svc $run%';" | Out-Null
    Remove-FixtureMerchant $teamId
    $http.Dispose()
}
