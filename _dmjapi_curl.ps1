#@'
#{
#  "lines": [
#    { "entity":"OCPR", "field":"Tel1", "oldValue":"931111111", "newValue":"932222225", "isSensitive": false }
#  ]
#}
#'@ | Set-Content -LiteralPath ".\body_update_lines.json" -Encoding UTF8


$key = "rDG01rwHQ5r4wqZUowG9xlRYX_PIDBtQmLyw441-KcI"
$ref = "CRQ-20260203-1000-5F2B9C"
$url = "http://10.10.60.14:8086/api/SapChangeRequestsUdo/create"

#$args = @(
#  "-i","-v",
#  "-X","POST",$url,
#  "-H","accept: application/json",
#  "-H","X-Api-Key: $key",
#  "-H","X-DMJ-Debug: 1",
#  "-H","Content-Type: application/json",
#  "--data-binary","@crq_create.json"
#)

@'
{
  "lines": [
    { "entity":"OCPR", "field":"Tel1", "oldValue":"931111111", "newValue":"932222255", "isSensitive": false }
  ]
}
'@ | Set-Content -LiteralPath ".\body_update_lines.json" -Encoding UTF8

curl.exe -sS -v -X POST "http://10.10.60.14:8086/api/SapChangeRequestsUdo/update-lines/CRQ-20260203-1000-5F2B9C" `
  -H "accept: application/json" `
  -H "X-Api-Key: $key" `
  -H "Content-Type: application/json" `
  --data-binary "@body_update_lines.json"



# executar amb ./_dmjapi_curl.ps1