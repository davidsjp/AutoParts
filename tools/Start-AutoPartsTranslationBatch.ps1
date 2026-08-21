param(
    [string]$ApiBaseUrl = "http://localhost:5080",
    [int]$BatchSize = 40000
)

$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "..\src\AutoParts.Api\AutoParts.Api.csproj"
$artifactRoot = Join-Path $PSScriptRoot "..\artifacts\openai-translation-batches"
$logPath = Join-Path $artifactRoot "start.log"
New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null

function Write-ProgressLog([string]$message) {
    "$(Get-Date -Format o) $message" | Add-Content -LiteralPath $logPath -Encoding utf8
}

try {
    $secretLines = & dotnet user-secrets list --project $project 2>$null
    $keyLine = $secretLines | Where-Object { $_ -match '^OpenAI:ApiKey\s*=\s*' } | Select-Object -First 1
    $apiKey = if ($keyLine) { ($keyLine -replace '^OpenAI:ApiKey\s*=\s*', '').Trim() } else { $env:OPENAI_API_KEY }
    if ([string]::IsNullOrWhiteSpace($apiKey)) { throw "Chave da OpenAI não configurada nos segredos locais." }

    Write-ProgressLog "Lendo o catálogo local."
    $parts = Invoke-RestMethod -Uri "$ApiBaseUrl/api/parts" -TimeoutSec 600
    $englishTerms = '(?i)\b(with|for|left|right|front|rear|engine|door|cover|bracket|switch|sensor|housing|mount|kit|set|module|control|trim|wiring|pipe|support|connector|panel|frame|shaft|lever|guide|bearing|seal|gasket|actuator|fitting|clip|holder)\b'
    $commercialExclusions = '(?i)\b(parafuso|porca|mangueira|screw|bolt|nut|hose)\b'
    $pending = @($parts | Where-Object {
        $_.description -match $englishTerms -and $_.description -notmatch $commercialExclusions
    })
    Write-ProgressLog "Encontrados $($pending.Count) títulos pendentes; itens excluídos comercialmente não foram enviados."

    Add-Type -AssemblyName System.Net.Http
    $client = [System.Net.Http.HttpClient]::new()
    $client.DefaultRequestHeaders.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new("Bearer", $apiKey)
    $instruction = "Traduza o título de uma peça automotiva BMW para português brasileiro. Retorne somente o título em português, sem aspas ou explicação. Preserve integralmente números OEM, códigos BMW, siglas técnicas e referências. Traduza posição e orientação (left/right/front/rear), mas não invente aplicação, marca ou especificação."
    $model = "gpt-5-mini"
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $jobs = [System.Collections.Generic.List[object]]::new()

    for ($offset = 0; $offset -lt $pending.Count; $offset += $BatchSize) {
        $count = [Math]::Min($BatchSize, $pending.Count - $offset)
        $chunkNumber = [int]($offset / $BatchSize) + 1
        $jsonlPath = Join-Path $artifactRoot "traducoes-$stamp-$chunkNumber.jsonl"
        $writer = [System.IO.StreamWriter]::new($jsonlPath, $false, [System.Text.UTF8Encoding]::new($false))
        try {
            for ($position = $offset; $position -lt ($offset + $count); $position++) {
                $part = $pending[$position]
                $request = [ordered]@{
                    custom_id = "part-$($part.id)"
                    method = "POST"
                    url = "/v1/responses"
                    body = [ordered]@{
                        model = $model
                        instructions = $instruction
                        input = "OEM: $($part.oemPartNumber)`nTítulo: $($part.description)"
                        max_output_tokens = 120
                    }
                }
                $writer.WriteLine(($request | ConvertTo-Json -Compress -Depth 8))
            }
        }
        finally { $writer.Dispose() }

        Write-ProgressLog "Enviando lote $chunkNumber com $count títulos."
        $form = [System.Net.Http.MultipartFormDataContent]::new()
        $stream = [System.IO.File]::OpenRead($jsonlPath)
        $fileContent = [System.Net.Http.StreamContent]::new($stream)
        $fileContent.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse("application/jsonl")
        $form.Add($fileContent, "file", [System.IO.Path]::GetFileName($jsonlPath))
        $form.Add([System.Net.Http.StringContent]::new("batch"), "purpose")
        $uploadResponse = $client.PostAsync("https://api.openai.com/v1/files", $form).GetAwaiter().GetResult()
        $uploadContent = $uploadResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        if (-not $uploadResponse.IsSuccessStatusCode) { throw "Upload do lote recusado: HTTP $([int]$uploadResponse.StatusCode) - $uploadContent" }
        $upload = $uploadContent | ConvertFrom-Json

        $batchRequest = @{
            input_file_id = $upload.id
            endpoint = "/v1/responses"
            completion_window = "24h"
            metadata = @{ job = "autoparts-ptbr-title-translation"; chunk = "$chunkNumber"; submitted_at = $stamp }
        } | ConvertTo-Json -Compress -Depth 5
        $batchResponse = $client.PostAsync("https://api.openai.com/v1/batches", [System.Net.Http.StringContent]::new($batchRequest, [System.Text.Encoding]::UTF8, "application/json")).GetAwaiter().GetResult()
        $batchContent = $batchResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        if (-not $batchResponse.IsSuccessStatusCode) { throw "Criação do job recusada: HTTP $([int]$batchResponse.StatusCode) - $batchContent" }
        $batch = $batchContent | ConvertFrom-Json

        $metadata = [ordered]@{
            BatchId = $batch.id
            InputFileId = $upload.id
            Status = $batch.status
            Model = $model
            Records = $count
            SubmittedAt = (Get-Date).ToString("o")
            JsonlFile = $jsonlPath
        }
        $metadataPath = "$jsonlPath.metadata.json"
        $metadata | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $metadataPath -Encoding utf8
        $jobs.Add([PSCustomObject]$metadata)
        Write-ProgressLog "Lote $chunkNumber criado: $($batch.id), status $($batch.status)."
    }

    $jobs | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $artifactRoot "jobs-$stamp.json") -Encoding utf8
    Write-ProgressLog "Envio concluído: $($jobs.Count) lote(s), $($pending.Count) títulos."
}
catch {
    Write-ProgressLog "ERRO: $($_.Exception.Message)"
    throw
}
