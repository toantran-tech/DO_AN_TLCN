param([string]$BaseUrl='http://localhost:5000', [string]$PostgresContainer='washgo_postgres', [string]$PostgresUser='postgres', [string]$Database='washgo_db')
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly
Add-Type -AssemblyName System.Net.Http
Add-Type -AssemblyName System.IO.Compression
$fixture=[Guid]::NewGuid(); $files=@(); $sessions=@()
function Upload-Doc($Bytes,$Name,$Kind,$Token,$Expected=201) {
    $client=[Net.Http.HttpClient]::new()
    $form=[Net.Http.MultipartFormDataContent]::new()
    try {
        $client.DefaultRequestHeaders.Authorization=[Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer',$Token)
        $form.Add([Net.Http.ByteArrayContent]::new([byte[]]$Bytes),'file',$Name)
        $form.Add([Net.Http.StringContent]::new($Kind),'kind')
        $response=$client.PostAsync("$BaseUrl/files/documents/",$form).GetAwaiter().GetResult()
        $text=$response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        if([int]$response.StatusCode -ne $Expected){throw "Upload expected $Expected got $([int]$response.StatusCode)"}
        if($Expected -eq 201){return ($text|ConvertFrom-Json).data}
    } finally {$form.Dispose();$client.Dispose()}
}
function New-Docx {
    $memory=[IO.MemoryStream]::new(); $zip=[IO.Compression.ZipArchive]::new($memory,[IO.Compression.ZipArchiveMode]::Create,$true)
    foreach($part in @(
        @('word/document.xml','<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body><w:p/></w:body></w:document>'),
        @('[Content_Types].xml','<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>')
    )) {$entry=$zip.CreateEntry($part[0]);$writer=[IO.StreamWriter]::new($entry.Open());$writer.Write($part[1]);$writer.Dispose()}
    $zip.Dispose(); $bytes=$memory.ToArray();$memory.Dispose();return ,$bytes
}
try {
    $a=(Call-Api Post '/auth/login' @{email='admin@washgo.vn';password='Admin@123'} $null).data;$sessions+= $a;$admin=$a.accessToken
    $source=Sql "SELECT id FROM washgo.users WHERE email='merchant@washgo.vn';"
    Sql @"
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object('id','$fixture','email','doc-$fixture@example.test','provider_id',NULL))).* FROM washgo.users u WHERE id='$source';
INSERT INTO washgo.merchants SELECT (jsonb_populate_record(NULL::washgo.merchants,to_jsonb(m)||jsonb_build_object('user_id','$fixture','tax_code',NULL,'status',1,'business_license_document_id',NULL,'contract_document_id',NULL,'sla_document_id',NULL))).* FROM washgo.merchants m WHERE user_id='$source';
"@|Out-Null
    $m=(Call-Api Post '/auth/login' @{email="doc-$fixture@example.test";password='Merchant@123'} $null).data;$sessions+=$m;$merchant=$m.accessToken
    $other=(Call-Api Post '/auth/login' @{email='merchant@washgo.vn';password='Merchant@123'} $null).data;$sessions+=$other
    $bytes=New-Docx
    foreach($extension in @('pdf','doc')) {
        $fixturePath=Join-Path $PSScriptRoot "PrivateDocuments/fixtures/fixture.$extension"
        if(Test-Path -LiteralPath $fixturePath){$uploaded=Upload-Doc ([IO.File]::ReadAllBytes($fixturePath)) "valid.$extension" 'Contract' $admin;$files+=$uploaded.fileId}
    }
    Upload-Doc ([byte[]]::new(10*1024*1024+1)) 'oversize.docx' 'Contract' $admin 400
    Upload-Doc ([Text.Encoding]::UTF8.GetBytes('not a document')) 'fake.pdf' 'Sla' $admin 400
    Upload-Doc $bytes 'wrong.pdf' 'Contract' $admin 400
    Upload-Doc $bytes 'contract.docx' 'Contract' $merchant 403
    $license=Upload-Doc $bytes 'license.docx' 'BusinessLicense' $merchant;$files+=$license.fileId
    $sla=Upload-Doc $bytes 'sla.docx' 'Sla' $admin;$files+=$sla.fileId
    Assert-Status 401 Get "/files/documents/$($license.fileId)" $null $null
    Assert-Status 404 Get "/files/documents/$($license.fileId)" $null $other.accessToken
    Assert-Status 404 Get "/files/documents/$($sla.fileId)" $null $merchant
    $view=(Call-Api Get "/merchant/$fixture" $null $admin).data
    $body=@{id="$fixture";expectedVersion=$view.version;slaDocumentId=$sla.fileId}
    foreach($key in @('businessName','address','district','city','latitude','longitude','phone','depositAmount','commissionRate','serviceRadiusKm','maxCapacity','openingHours')){$body[$key]=$view.$key}
    $saved=(Call-Api Put '/merchant/' $body $admin).data
    if($saved.slaDocumentId -ne $sla.fileId){throw 'SLA link missing.'}
    Call-Api Get "/files/documents/$($sla.fileId)" $null $merchant|Out-Null
    Assert-Status 400 Delete "/files/documents/$($sla.fileId)" $null $admin
    $body.expectedVersion=$saved.version;$body.Remove('slaDocumentId')
    $saved=(Call-Api Put '/merchant/' $body $admin).data
    if($saved.slaDocumentId -ne $sla.fileId){throw 'Missing id cleared file.'}
    $profile=@{businessName=$saved.businessName;address=$saved.address;city=$saved.city;phone=$saved.phone;openingHours=$saved.openingHours;businessLicenseDocumentId=$license.fileId;operationId=[Guid]::NewGuid().ToString()}
    $request=(Call-Api Post '/merchant-profile/update-requests' $profile $merchant).data
    $replay=(Call-Api Post '/merchant-profile/update-requests' $profile $merchant).data
    if($request.id -ne $replay.id){throw 'Submit replay duplicated request.'}
    Assert-Status 400 Delete "/files/documents/$($license.fileId)" $null $merchant
    Call-Api Post "/merchant-profile-request/$($request.id)/approve?expectedVersion=$($request.version)" $null $admin|Out-Null
    $approved=(Call-Api Get "/merchant/$fixture" $null $admin).data
    if($approved.businessLicenseDocumentId -ne $license.fileId -or $approved.slaDocumentId -ne $sla.fileId){throw 'Approval changed wrong document.'}
    $http=[Net.Http.HttpClient]::new()
    try {
        $http.DefaultRequestHeaders.Authorization=[Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer',$merchant)
        $response=$http.GetAsync("$BaseUrl/files/documents/$($license.fileId)/download").GetAwaiter().GetResult()
        $download=$response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
        if([int]$response.StatusCode -ne 200 -or $download.Length -ne $bytes.Length -or !$response.Content.Headers.ContentDisposition){throw 'Authenticated download failed.'}
    } finally {$http.Dispose()}
    $draft=Upload-Doc $bytes 'expired.docx' 'BusinessLicense' $merchant;$files+=$draft.fileId
    Sql "UPDATE washgo.private_documents SET expires_at=now()-interval '1 day' WHERE id='$($draft.fileId)';"|Out-Null
    Call-Api Post '/files/documents/cleanup' $null $admin|Out-Null
    Assert-Status 404 Get "/files/documents/$($draft.fileId)" $null $merchant
    Call-Api Get "/files/documents/$($license.fileId)" $null $merchant|Out-Null
    $body.expectedVersion=$approved.version;$body.removeSlaDocument=$true
    $removed=(Call-Api Put '/merchant/' $body $admin).data
    if($removed.slaDocumentId){throw 'Explicit remove failed.'}
    Call-Api Get "/files/documents/$($sla.fileId)" $null $merchant|Out-Null
    Write-Output 'PASS: content validation, role/owner authorization, private download, explicit attach/keep/remove, approval and submit replay, referenced history retained, draft expiry cleanup.'
} finally {
    if($files.Count){$ids=($files|ForEach-Object{"'$_'"})-join ',';Sql "UPDATE washgo.private_documents SET referenced_at=NULL,expires_at=now()-interval '1 day' WHERE id IN ($ids);"|Out-Null;if($admin){Call-Api Post '/files/documents/cleanup' $null $admin|Out-Null}}
    Sql "DELETE FROM washgo.merchant_profile_requests WHERE merchant_id='$fixture'; DELETE FROM washgo.merchant_write_operations WHERE merchant_id='$fixture'; DELETE FROM washgo.users WHERE id='$fixture';"|Out-Null
    foreach($s in $sessions){try{Call-Api Post '/auth/logout' @{refreshToken=$s.refreshToken} $s.accessToken|Out-Null}catch{}}
}
