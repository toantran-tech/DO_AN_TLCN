param([string]$BaseUrl='http://localhost:5000',[string]$PostgresContainer='washgo_postgres',[string]$PostgresUser='postgres',[string]$Database='washgo_db')
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly
Add-Type -AssemblyName System.Net.Http
$session=$null;$urls=@();$service=$null;$run=[Guid]::NewGuid().ToString('N').Substring(0,10)
function Upload {
    $client=[Net.Http.HttpClient]::new();$form=[Net.Http.MultipartFormDataContent]::new()
    try {
        $client.DefaultRequestHeaders.Authorization=[Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer',$admin)
        $png=[Convert]::FromBase64String('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl6ijAAAAAASUVORK5CYII=')
        $form.Add([Net.Http.ByteArrayContent]::new($png),'file','pixel.png')
        $response=$client.PostAsync("$BaseUrl/files/images",$form).GetAwaiter().GetResult()
        if([int]$response.StatusCode -ne 201){throw "Upload returned $([int]$response.StatusCode)"}
        return ($response.Content.ReadAsStringAsync().GetAwaiter().GetResult()|ConvertFrom-Json).data.url
    } finally {$form.Dispose();$client.Dispose()}
}
try {
    $session=(Call-Api Post '/auth/login' @{email='admin@washgo.vn';password='Admin@123'} $null).data;$admin=$session.accessToken
    $kept=Upload;$urls+=$kept;$draft=Upload;$urls+=$draft;$expired=Upload;$urls+=$expired
    $service=(Call-Api Post '/wash-service/' @{code="MEDIA_$run";name="Media $run";unitPrice=20000;unit='item';serviceType=1;estimatedDurationHours=24;imageUrls=@($kept)} $admin).data
    if((Sql "SELECT referenced_at IS NOT NULL FROM washgo.uploaded_assets WHERE url='$kept';") -ne 't'){throw 'Attachment reference not committed.'}
    Sql "UPDATE washgo.uploaded_assets SET expires_at=now()-interval '1 day' WHERE url IN ('$kept','$draft','$expired');"|Out-Null
    $body=@{code="EXPIRED_$run";name="Expired $run";unitPrice=20000;unit='item';serviceType=1;estimatedDurationHours=24;imageUrls=@($expired)}
    Assert-Status 400 Post '/wash-service/' $body $admin
    if((Sql "SELECT COUNT(*) FROM washgo.services WHERE code='EXPIRED_$run';") -ne '0'){throw 'Expired attachment left a partial record.'}
    Call-Api Post '/files/media/cleanup' $null $admin|Out-Null
    if((Sql "SELECT is_deleted FROM washgo.uploaded_assets WHERE url='$kept';") -ne 'f'){throw 'Cleanup removed referenced file.'}
    foreach($url in @($draft,$expired)){Assert-Status 404 Get $url $null $null}
    $response=Invoke-WebRequest -UseBasicParsing -Uri "$BaseUrl$kept"
    if($response.StatusCode -ne 200){throw 'Referenced upload unavailable.'}
    Write-Output 'PASS: upload registry, atomic reference retention, expired attachment 400 with rollback, orphan cleanup, referenced file preserved.'
} finally {
    if($service){Sql "DELETE FROM washgo.services WHERE id='$($service.id)';"|Out-Null}
    if($urls.Count){$values=($urls|ForEach-Object{"'$_'"})-join ',';Sql "UPDATE washgo.uploaded_assets SET referenced_at=NULL,expires_at=now()-interval '1 day' WHERE url IN ($values);"|Out-Null;if($admin){Call-Api Post '/files/media/cleanup' $null $admin|Out-Null}}
    if($session){Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $admin|Out-Null}
}
