# Run from the repository root: .\scripts\demo.ps1
# Creates its own demo records. Does not delete existing user data.
param([switch]$VerifyFailure, [switch]$VerifyPersistence)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
$client = New-Object System.Net.Http.HttpClient
$client.Timeout = [TimeSpan]::FromSeconds(65)
New-Item -ItemType Directory -Path (Join-Path $PSScriptRoot '../docs') -Force | Out-Null
$checks = [System.Collections.Generic.List[object]]::new()
$label = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,4)
$start = [DateTimeOffset]::UtcNow.AddDays(1)
$end = $start.AddHours(2)

function Send-Api([int]$Port, [string]$Method, [string]$Path, $Body = $null) {
    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::new($Method), "http://localhost:$Port$Path")
    if ($null -ne $Body) {
        $request.Content = [System.Net.Http.StringContent]::new(($Body | ConvertTo-Json -Depth 12 -Compress), [Text.Encoding]::UTF8, 'application/json')
    }
    try {
        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        try {
            $content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            $data = if ($content) { $content | ConvertFrom-Json } else { $null }
            return [PSCustomObject]@{ Status = [int]$response.StatusCode; Data = $data }
        } finally { $response.Dispose() }
    } finally { $request.Dispose() }
}
function Check([string]$Name, [int]$Port, [string]$Method, [string]$Path, $Body, [int]$Expected) {
    $result = Send-Api $Port $Method $Path $Body
    if ($result.Status -ne $Expected) { throw "$Name expected $Expected, got $($result.Status): $($result.Data | ConvertTo-Json -Depth 8 -Compress)" }
    $checks.Add([PSCustomObject]@{ Check=$Name; Method=$Method; Path=$Path; Status=$result.Status })
    Write-Host "PASS $Name ($($result.Status))" -ForegroundColor Green
    return $result.Data
}
function Assert-Value([string]$Name, $Actual, $Expected) {
    if ($Actual -ne $Expected) { throw "$Name expected '$Expected', got '$Actual'." }
    $checks.Add([PSCustomObject]@{ Check=$Name; Actual=$Actual; Expected=$Expected })
    Write-Host "PASS $Name" -ForegroundColor Green
}
function New-TournamentBody([string]$Name, [string]$Type, [int]$Capacity, [DateTimeOffset]$From, [DateTimeOffset]$To) {
    return @{name="$Name $label"; type=$Type; description='AUBG campus demo'; startDate=$From.ToString('o'); endDate=$To.ToString('o');
        location='AUBG Sports Center'; capacity=$Capacity; firstPrize=100; secondPrize=50; thirdPrize=25}
}
function Wait-Services {
    foreach ($port in @(5001,5002,5003)) {
        $ready = $false
        for ($attempt = 0; $attempt -lt 60; $attempt++) {
            try { if ((Send-Api $port GET '/health').Status -eq 200) { $ready=$true; break } } catch { }
            Start-Sleep -Seconds 1
        }
        if (!$ready) { throw "Service on port $port is unavailable. Start Docker Compose and check logs." }
    }
}
try {
    Wait-Services
    foreach ($port in @(5001,5002,5003)) {
        $null = Check "Health $port" $port GET '/health' $null 200
        $null = Check "Swagger $port" $port GET '/swagger/v1/swagger.json' $null 200
    }
    $people = @(1..5 | ForEach-Object { Check "Create participant $_" 5002 POST '/api/participants' @{ name="Demo Person $_ $label" } 201 })
    $basketballBody = New-TournamentBody 'Basketball Cup' Basketball 3 $start $end
    $basketball = Check 'Create basketball' 5001 POST '/api/tournaments' $basketballBody 201
    $football = Check 'Create overlapping football' 5001 POST '/api/tournaments' (New-TournamentBody 'Football Cup' Football 5 $start.AddMinutes(30) $end.AddMinutes(30)) 201
    $swimming = Check 'Create adjacent swimming' 5001 POST '/api/tournaments' (New-TournamentBody 'Swimming Cup' Swimming 5 $end $end.AddHours(1)) 201
    Assert-Value 'New status is Upcoming' $basketball.status Upcoming

    $null = Check 'Unknown tournament' 5003 POST '/api/registrations' @{tournamentId=2147483647;participantId=$people[0].id} 404
    $null = Check 'Unknown participant' 5003 POST '/api/registrations' @{tournamentId=$basketball.id;participantId=2147483647} 404
    $null = Check 'Finish requires winners' 5001 POST "/api/tournaments/$($basketball.id)/finish" $null 409
    foreach ($person in $people[0..2]) {
        $null = Check "Register basketball person $($person.id)" 5003 POST '/api/registrations' @{tournamentId=$basketball.id; participantId=$person.id} 201
    }
    $null = Check 'Duplicate registration' 5003 POST '/api/registrations' @{tournamentId=$basketball.id;participantId=$people[0].id} 409
    $null = Check 'Capacity enforced' 5003 POST '/api/registrations' @{tournamentId=$basketball.id;participantId=$people[3].id} 409
    $null = Check 'Overlap enforced' 5003 POST '/api/registrations' @{tournamentId=$football.id;participantId=$people[0].id} 409
    $null = Check 'Back-to-back accepted' 5003 POST '/api/registrations' @{tournamentId=$swimming.id;participantId=$people[0].id} 201
    $null = Check 'Occupied Upcoming delete forbidden' 5001 DELETE "/api/tournaments/$($basketball.id)" $null 409
    $participants = Check 'Upcoming participants with counts' 5001 GET "/api/tournaments/$($basketball.id)/participants" $null 200
    Assert-Value 'Three participants listed' @($participants).Count 3
    $basketballBody.location = 'Updated AUBG Gym'
    $updated = Check 'Update allowed location' 5001 PUT "/api/tournaments/$($basketball.id)" $basketballBody 200
    Assert-Value 'Location persisted' $updated.location 'Updated AUBG Gym'
    $basketballBody.capacity=2
    $null = Check 'Cannot reduce capacity below signups' 5001 PUT "/api/tournaments/$($basketball.id)" $basketballBody 409
    $basketballBody.capacity=3; $basketballBody.startDate=$start.AddMinutes(10).ToString('o')
    $null = Check 'Occupied schedule is immutable' 5001 PUT "/api/tournaments/$($basketball.id)" $basketballBody 409

    $bad = New-TournamentBody 'Bad Dates' Football 3 $start $start
    $null = Check 'End must be after start' 5001 POST '/api/tournaments' $bad 400
    $bad.startDate=[DateTimeOffset]::UtcNow.AddDays(-1).ToString('o'); $bad.endDate=$end.ToString('o')
    $null = Check 'Past start rejected' 5001 POST '/api/tournaments' $bad 400
    $bad.startDate=$start.ToString('o'); $bad.capacity=0
    $null = Check 'Zero capacity rejected' 5001 POST '/api/tournaments' $bad 400
    $bad.capacity=3; $bad.firstPrize=-1
    $null = Check 'Negative prize rejected' 5001 POST '/api/tournaments' $bad 400
    $bad.firstPrize=1.001
    $null = Check 'Fractional cent rejected' 5001 POST '/api/tournaments' $bad 400
    $bad.firstPrize=100; $bad.type='Chess'
    $null = Check 'Unknown sport rejected' 5001 POST '/api/tournaments' $bad 400
    $null = Check 'Blank participant rejected' 5002 POST '/api/participants' @{name='  '} 400

    $winners = @{ winners=@(
        @{participantId=$people[0].id;place=1;points=30},
        @{participantId=$people[1].id;place=2;points=20},
        @{participantId=$people[2].id;place=3;points=10}) }
    $winners.winners[2].participantId=$people[3].id
    $null = Check 'Unregistered winner rejected' 5001 PUT "/api/tournaments/$($basketball.id)/winners" $winners 409
    $winners.winners[2].participantId=$people[0].id
    $null = Check 'Duplicate winner rejected' 5001 PUT "/api/tournaments/$($basketball.id)/winners" $winners 400
    $winners.winners[2].participantId=$people[2].id
    $official = Check 'Submit winners' 5001 PUT "/api/tournaments/$($basketball.id)/winners" $winners 200
    Assert-Value 'First prize is USD 100' $official[0].prizeUsd 100
    $official = Check 'Replace provisional winners atomically' 5001 PUT "/api/tournaments/$($basketball.id)/winners" $winners 200
    $finished = Check 'Finish basketball' 5001 POST "/api/tournaments/$($basketball.id)/finish" $null 200
    Assert-Value 'Finished status' $finished.status Finished
    $null = Check 'Finished delete forbidden' 5001 DELETE "/api/tournaments/$($basketball.id)" $null 409
    $null = Check 'Finished cancel forbidden' 5001 POST "/api/tournaments/$($basketball.id)/cancel" $null 409
    $null = Check 'Finished winners immutable' 5001 PUT "/api/tournaments/$($basketball.id)/winners" $winners 409
    $null = Check 'Finished signup forbidden' 5003 POST '/api/registrations' @{tournamentId=$basketball.id;participantId=$people[3].id} 409
    $stats = Check 'Participant statistics' 5002 GET "/api/participants/$($people[0].id)/statistics" $null 200
    Assert-Value 'Finished participation count' $stats.participationCount 1
    Assert-Value 'Upcoming registration count' $stats.upcomingRegistrations 1
    $null = Check 'Read official winners' 5001 GET "/api/tournaments/$($basketball.id)/winners" $null 200
    $null = Check 'Filter Finished basketball' 5001 GET '/api/tournaments?status=Finished&type=Basketball' $null 200
    $null = Check 'Read participant history' 5003 GET "/api/registrations/participant/$($people[0].id)" $null 200

    $null = Check 'Cancel swimming' 5001 POST "/api/tournaments/$($swimming.id)/cancel" $null 200
    $null = Check 'Canceled signup forbidden' 5003 POST '/api/registrations' @{tournamentId=$swimming.id;participantId=$people[3].id} 409
    $replacement = Check 'Create replacement swimming' 5001 POST '/api/tournaments' (New-TournamentBody 'Swimming Replacement' Swimming 3 $end $end.AddHours(1)) 201
    $null = Check 'Cancellation releases schedule' 5003 POST '/api/registrations' @{tournamentId=$replacement.id;participantId=$people[0].id} 201
    $null = Check 'Canceled tournament can be deleted' 5001 DELETE "/api/tournaments/$($swimming.id)" $null 204
    $history = Check 'Deleted canceled tournament keeps readable registration' 5003 GET "/api/registrations/participant/$($people[0].id)" $null 200
    Assert-Value 'Deleted historical entry preserved' @($history | Where-Object tournamentStatus -eq Deleted).Count 1
    $empty = Check 'Create empty tournament' 5001 POST '/api/tournaments' (New-TournamentBody 'Empty Cup' Football 3 $start.AddDays(1) $end.AddDays(1)) 201
    $null = Check 'Empty Upcoming can be deleted' 5001 DELETE "/api/tournaments/$($empty.id)" $null 204

    # Two asynchronous requests compete for one spot, using the real MySQL named lock.
    $oneSlot = Check 'Create one-slot tournament' 5001 POST '/api/tournaments' (New-TournamentBody 'One Slot Cup' Football 1 $start.AddDays(2) $end.AddDays(2)) 201
    $requests=@(); $tasks=@()
    foreach ($person in $people[3..4]) {
        $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Post, 'http://localhost:5003/api/registrations')
        $request.Content=[System.Net.Http.StringContent]::new((@{tournamentId=$oneSlot.id;participantId=$person.id}|ConvertTo-Json),[Text.Encoding]::UTF8,'application/json')
        $requests += $request; $tasks += $client.SendAsync($request)
    }
    $responses = @($tasks | ForEach-Object { $_.GetAwaiter().GetResult() })
    $statuses = @($responses | ForEach-Object {[int]$_.StatusCode} | Sort-Object)
    Assert-Value 'Concurrent signups: one success, one conflict' ($statuses -join ',') '201,409'
    $responses | ForEach-Object Dispose; $requests | ForEach-Object Dispose

    if ($VerifyFailure) {
        try {
            docker compose stop participantservice
            if ($LASTEXITCODE -ne 0) { throw 'Could not stop Participant service.' }
            $null = Check 'Dependency outage returns 503' 5003 POST '/api/registrations' @{tournamentId=$football.id;participantId=$people[3].id} 503
            $entries = Check 'Read registrations after failed signup' 5003 GET "/api/registrations/tournament/$($football.id)" $null 200
            Assert-Value 'Failed signup writes nothing' @($entries).Count 0
        } finally { docker compose start participantservice; if ($LASTEXITCODE -ne 0) { throw 'Could not restart Participant service.' }; Wait-Services }
        # Let the circuit breaker recover before the next dependent request.
        Start-Sleep -Seconds 6
        $null = Check 'Recovered service accepts signup' 5003 POST '/api/registrations' @{tournamentId=$football.id;participantId=$people[3].id} 201
    }
    if ($VerifyPersistence) {
        docker compose down
        if ($LASTEXITCODE -ne 0) { throw 'Compose down failed.' }
        docker compose up -d
        if ($LASTEXITCODE -ne 0) { throw 'Compose up failed.' }
        Wait-Services
        $saved = Check 'Tournament survives container recreation' 5001 GET "/api/tournaments/$($basketball.id)" $null 200
        Assert-Value 'Persisted status' $saved.status Finished
        $savedWinners = Check 'Winners survive container recreation' 5001 GET "/api/tournaments/$($basketball.id)/winners" $null 200
        Assert-Value 'Three persisted winners' @($savedWinners).Count 3
        $savedPeople = Check 'Participant survives container recreation' 5002 GET "/api/participants/$($people[0].id)" $null 200
        Assert-Value 'Participant name persisted' $savedPeople.name $people[0].name
        $savedRegistrations = Check 'Registrations survive container recreation' 5003 GET "/api/registrations/tournament/$($basketball.id)" $null 200
        Assert-Value 'Three persisted registrations' @($savedRegistrations).Count 3
    }
    $report = [PSCustomObject]@{
        TestedAtUtc=[DateTimeOffset]::UtcNow.ToString('o'); Passed=$checks.Count; Failed=0;
        FailureTest=[bool]$VerifyFailure; PersistenceTest=[bool]$VerifyPersistence;
        DemoIds=@{finishedTournament=$basketball.id;upcomingTournament=$football.id;participant=$people[0].id}; Checks=$checks
    }
    $report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $PSScriptRoot '../docs/verification-results.json') -Encoding UTF8
    Write-Host "All $($checks.Count) checks passed. Demo IDs: $($report.DemoIds | ConvertTo-Json -Compress)" -ForegroundColor Cyan
} finally { $client.Dispose() }

