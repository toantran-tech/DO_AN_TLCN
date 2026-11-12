# LOCAL DEV ONLY helpers for Merchant shipper-management tests. Dot-source after order-security.ps1 -HelpersOnly.
# A separate Merchant per run keeps team counts and sort orders independent of seeded data.
if (-not $MailpitUrl) { $MailpitUrl = 'http://localhost:8025' }

function New-FixtureMerchant($Run) {
    $seedId = Sql "SELECT id FROM washgo.users WHERE email='merchant@washgo.vn';"
    $id = [Guid]::NewGuid()
    Sql @"
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$id','email','team-$Run@example.com','full_name','Team Smoke Merchant $Run','phone',NULL,'auth_provider',1,'provider_id',NULL,'is_active',true,'avatar',NULL))).* FROM washgo.users u WHERE id='$seedId';
INSERT INTO washgo.merchants SELECT (jsonb_populate_record(NULL::washgo.merchants,to_jsonb(m)||jsonb_build_object(
'user_id','$id','business_name','Team Smoke Laundry $Run','tax_code',NULL,'status',1))).* FROM washgo.merchants m WHERE user_id='$seedId';
"@ | Out-Null
    (Call-Api Post '/auth/login' @{email="team-$Run@example.com";password='Merchant@123'} $null).data
}

function New-FixtureShipper($Token, $Name, $Run) {
    $phone = '09' + (Get-Random -Minimum 10000000 -Maximum 99999999)
    $email = "ship-$Run-$(Get-Random)@example.com"
    $body = @{fullName=$Name;phone=$phone;email=$email;vehicleType=1;vehiclePlate='59-X1 123.45'}
    (Call-Api Post '/shipper/' $body $Token).data
}

function Read-TempPassword($Email) {
    for ($i = 0; $i -lt 40; $i++) {
        $found = Invoke-RestMethod -Method Get -Uri "$MailpitUrl/api/v1/search?query=$([uri]::EscapeDataString("to:$Email"))"
        if ($found.messages.Count) {
            $msg = Invoke-RestMethod -Method Get -Uri "$MailpitUrl/api/v1/message/$($found.messages[0].ID)"
            # Plain template ": XXXXXXXXXXXX" or the HTML-to-text template ": *XXXXXXXXXXXX*".
            if ($msg.Text -match '(?m):\s*\*?([A-Za-z0-9]{12})\*?\s*$') { return $Matches[1] }
        }
        Start-Sleep -Milliseconds 300
    }
    throw "No credentials email for $Email."
}

function Upload-TestImage($Token) {
    Add-Type -AssemblyName System.Net.Http
    $client = New-Object System.Net.Http.HttpClient
    try {
        $client.DefaultRequestHeaders.Authorization = New-Object System.Net.Http.Headers.AuthenticationHeaderValue('Bearer', $Token)
        $bytes = New-Object byte[] 64
        [byte[]](0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A) | ForEach-Object -Begin {$i=0} -Process {$bytes[$i]=$_;$i++}
        $bytes[20] = [byte](Get-Random -Maximum 255)
        $form = New-Object System.Net.Http.MultipartFormDataContent
        $content = New-Object System.Net.Http.ByteArrayContent(,$bytes)
        $content.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('image/png')
        $form.Add($content, 'file', 'identity.png')
        $body = $client.PostAsync("$BaseUrl/files/images", $form).Result.Content.ReadAsStringAsync().Result
        return ($body | ConvertFrom-Json).data.url
    } finally { $client.Dispose() }
}

function Remove-FixtureMerchant($MerchantId) {
    if (-not $MerchantId) { return }
    Sql @"
UPDATE washgo.users SET is_active=false WHERE id IN (SELECT user_id FROM washgo.shippers WHERE merchant_id='$MerchantId');
UPDATE washgo.shippers SET is_active=false WHERE merchant_id='$MerchantId';
UPDATE washgo.users SET is_active=false WHERE id='$MerchantId';
UPDATE washgo.merchants SET status=3 WHERE user_id='$MerchantId';
"@ | Out-Null
}
