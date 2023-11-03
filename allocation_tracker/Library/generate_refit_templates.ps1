$url = 'https://app.swaggerhub.com/apiproxy/registry/DTN-FuelSuite/allocation-tracker/2.0?resolved=true&flatten=true&pretty=true'
$output = Join-Path $PSScriptRoot Swagger.API.RefitClient.cs
refitter $url -o $output -n Gravitate.API.Client --use-api-response --no-auto-generated-header

(Get-Content -path $output -Raw) -replace ' Task ', ' Task<IApiResponse<string>> '   | Set-Content -Path $output