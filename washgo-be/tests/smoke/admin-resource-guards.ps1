param([string]$BaseUrl='http://localhost:5000',[string]$PostgresContainer='washgo_postgres',[string]$PostgresUser='postgres',[string]$Database='washgo_db')
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly
$sessions=@();$service=$null;$locker=$null;$promotion=$null;$incident=[Guid]::NewGuid();$order=[Guid]::NewGuid();$keys=@();$run=[Guid]::NewGuid().ToString('N').Substring(0,10)
try {
    $a=(Call-Api Post '/auth/login' @{email='admin@washgo.vn';password='Admin@123'} $null).data;$sessions+=$a;$admin=$a.accessToken
    $m=(Call-Api Post '/auth/login' @{email='merchant@washgo.vn';password='Merchant@123'} $null).data;$sessions+=$m
    $c=(Call-Api Post '/auth/login' @{email='customer@washgo.vn';password='Customer@123'} $null).data;$sessions+=$c
    $service=(Call-Api Post '/wash-service/' @{code="GUARD_$run";name="Guard Service $run";unitPrice=30000;unit='item';serviceType=1;estimatedDurationHours=24;imageUrls=@()} $admin).data
    if(!$service.version){throw 'Service version missing.'}
    Assert-Status 400 Put '/wash-service/status' @{serviceId=$service.id;approvalStatus=2;isActive=$false} $admin
    $key=[Guid]::NewGuid().ToString();$keys+=$key
    $body=@{operationId=$key;action='Status';status=2;isActive=$false;items=@(@{id=$service.id;expectedVersion=$service.version},@{id=[Guid]::NewGuid().ToString();expectedVersion='missing'})}
    $batch=(Call-Api Post '/wash-service/batch' $body $admin).data
    if($batch.items.Count -ne 2 -or !$batch.items[0].succeeded -or $batch.items[1].statusCode -ne 404){throw 'Service batch partial success failed.'}
    $replay=(Call-Api Post '/wash-service/batch' $body $admin).data
    if(!$replay.items[0].succeeded){throw 'Batch replay lost original success.'}
    Call-Api Get "/admin-operations/Service/$key" $null $admin|Out-Null
    $body.isActive=$true;Assert-Status 409 Post '/wash-service/batch' $body $admin
    $promotion=(Call-Api Post '/promotion/' @{code="GUARD_$run";discountType=1;discountValue=10;minOrderAmount=0;startAt='2025-01-01T00:00:00Z';endAt='2028-01-01T00:00:00Z';isActive=$true} $admin).data
    $key=[Guid]::NewGuid().ToString();$keys+=$key
    $status=@{operationId=$key;expectedVersion=$promotion.version;isActive=$false}
    $off=(Call-Api Put "/promotion/$($promotion.id)/status" $status $admin).data
    Call-Api Put "/promotion/$($promotion.id)/status" $status $admin|Out-Null
    Assert-Status 409 Put "/promotion/$($promotion.id)/status" @{expectedVersion=$promotion.version;isActive=$true} $admin
    if($off.isActive){throw 'Promotion target status ignored.'}
    Call-Api Get "/promotion/$($promotion.id)/statistics" $null $admin|Out-Null
    $locker=(Call-Api Post '/locker/' @{merchantId=$m.user.id;code="GD_$run";name="Guard Locker $run";address='Local fixture';district='Test';city='Test';latitude=10.77;longitude=106.69;boxes=@(@{boxNumber='1';size=2})} $admin).data
    $key=[Guid]::NewGuid().ToString();$keys+=$key
    $status=@{expectedVersion=$locker.version;operationId=$key;status=2}
    $off=(Call-Api Put "/locker/$($locker.id)/status" $status $admin).data
    Call-Api Put "/locker/$($locker.id)/status" $status $admin|Out-Null
    Assert-Status 409 Put "/locker/$($locker.id)/status" @{expectedVersion=$locker.version;status=1} $admin
    Call-Api Put "/locker/$($locker.id)/status" @{expectedVersion=$off.version;status=1} $admin|Out-Null
    $template=Sql "SELECT id FROM washgo.orders LIMIT 1;"
    Sql @"
INSERT INTO washgo.orders SELECT (jsonb_populate_record(NULL::washgo.orders,to_jsonb(o)||jsonb_build_object('id','$order','order_code','IG_$run','merchant_order_code','IG_$run','customer_id','$($c.user.id)','merchant_id','$($m.user.id)','locker_id','$($locker.id)','locker_slot_id','$($locker.boxes[0].id)','slot_number','1','deposited_at','2026-01-02T00:00:00Z','return_locker_id',NULL,'status',12,'is_deleted',false))).* FROM washgo.orders o WHERE id='$template';
INSERT INTO washgo.incidents(id,order_id,order_code,customer_id,customer_name,type,severity,description,image_urls,video_urls,status,compensation_level,item_value,compensation_amount,compensation_status,created_on,modified_on,is_deleted,revision)
VALUES('$incident','$order','IG_$run','$($c.user.id)','Fixture',1,1,'Guard incident',ARRAY[]::text[],ARRAY[]::text[],1,0,0,0,0,1,1,false,0);
"@|Out-Null
    $filtered=Call-Api Post '/incident/filter' @{orderId="$order";customerId=$c.user.id;compensationStatus=0;sortBy=@(@{field='CreatedOn';isDescending=$true})} $admin
    if($filtered.total -ne 1 -or $filtered.list[0].id -ne "$incident"){throw 'Incident relation/compensation filters failed.'}
    Assert-Status 400 Post '/incident/filter' @{sortBy=@(@{field='unsupported'})} $admin
    $view=(Call-Api Get "/incident/$incident" $null $admin).data
    if($view.customerEmail -ne $c.user.email){throw 'Admin incident contact enrichment missing.'}
    if((Call-Api Get "/incident/$incident" $null $c.accessToken).data.customerEmail){throw 'Customer response exposed Admin-only contacts.'}
    $usage=(Call-Api Get "/locker/$($locker.id)/usage-history?page=1&take=1" $null $admin)
    if($usage.total -ne 1 -or $usage.list[0].usageId -ne "${order}:1"){throw 'DB usage paging lost the deposit use.'}
    $key=[Guid]::NewGuid().ToString();$keys+=$key
    $msg=@{content='Guard response';expectedVersion=$view.version;operationId=$key}
    Call-Api Post "/incident/$incident/messages" $msg $admin|Out-Null
    Call-Api Post "/incident/$incident/messages" $msg $admin|Out-Null
    $thread=(Call-Api Get "/incident/$incident/messages/page?page=1&take=1" $null $admin)
    if(@($thread.list).Count -ne 1 -or $thread.total -ne 1){throw 'Thread replay duplicated message or paging failed.'}
    $settings=(Call-Api Get '/system-setting' $null $admin).data
    Assert-Status 409 Post "/incident/$incident/resolve" @{expectedVersion=$view.version;expectedSettingsVersion=$settings.version;resolutionNote='Guard resolution';compensationLevel=1;itemValue=100000} $admin
    $view=(Call-Api Get "/incident/$incident" $null $admin).data
    $key=[Guid]::NewGuid().ToString();$keys+=$key
    $body=@{expectedVersion=$view.version;expectedSettingsVersion=$settings.version;resolutionNote='Guard resolution';compensationLevel=1;itemValue=100000;operationId=$key}
    $resolved=(Call-Api Post "/incident/$incident/resolve" $body $admin).data
    Call-Api Post "/incident/$incident/resolve" $body $admin|Out-Null
    if($resolved.compensationAmount -ne 100000*$settings.compensationLightPercent/100 -or $resolved.compensationSettingsVersion -ne $settings.version){throw 'Compensation basis not stored.'}
    $key=[Guid]::NewGuid().ToString();$keys+=$key
    $body=@{expectedVersion=$resolved.version;reference='test-compensation';operationId=$key}
    Call-Api Post "/incident/$incident/confirm-compensation" $body $admin|Out-Null
    Call-Api Post "/incident/$incident/confirm-compensation" $body $admin|Out-Null
    Assert-Status 409 Post "/incident/$incident/confirm-compensation" @{reference='different'} $admin
    foreach($path in @('/incident/summary','/wash-service/summary','/promotion/summary')){Call-Api Get $path $null $admin|Out-Null;Assert-Status 403 Get $path $null $m.accessToken}
    Write-Output 'PASS: service batch partial success/replay/lookup, promotion and locker status versions, incident thread revision/replay/paging, compensation snapshot/payment reference, summary authorization.'
} catch {Write-Output $_.ScriptStackTrace;throw}
finally {
    Sql "DELETE FROM washgo.incident_messages WHERE incident_id='$incident'; DELETE FROM washgo.incidents WHERE id='$incident'; DELETE FROM washgo.orders WHERE id='$order';"|Out-Null
    if($service){Sql "DELETE FROM washgo.services WHERE id='$($service.id)';"|Out-Null}
    if($promotion){Sql "DELETE FROM washgo.promotions WHERE id='$($promotion.id)';"|Out-Null}
    if($locker){Sql "DELETE FROM washgo.locker_merchants WHERE locker_id='$($locker.id)'; DELETE FROM washgo.locker_slots WHERE locker_id='$($locker.id)'; DELETE FROM washgo.lockers WHERE id='$($locker.id)';"|Out-Null}
    if($keys.Count){$ids=($keys|ForEach-Object{"'$_'"})-join ',';Sql "DELETE FROM washgo.administrative_operations WHERE id IN ($ids) OR resource_id IN ('$incident','$($service.id)','$($locker.id)','$($promotion.id)');"|Out-Null}
    foreach($s in $sessions){try{Call-Api Post '/auth/logout' @{refreshToken=$s.refreshToken} $s.accessToken|Out-Null}catch{}}
}
