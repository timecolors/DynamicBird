$listener = [System.Net.HttpListener]::new()
$listener.Prefixes.Add('http://localhost:8787/')
$listener.Start()
Write-Output 'MOCK LISTENING on 8787'
$logFile = Join-Path $PSScriptRoot 'mock-requests.log'
while ($true) {
  $ctx = $listener.GetContext()
  $req = $ctx.Request
  $resp = $ctx.Response
  $reader = [System.IO.StreamReader]::new($req.InputStream, $req.ContentEncoding)
  $body = $reader.ReadToEnd()
  try { Add-Content -Path $logFile -Value $body } catch {}
  $isStream = $body -match '"stream"\s*:\s*true'
  if ($isStream) {
    $resp.ContentType = 'text/event-stream'
    $resp.AddHeader('Cache-Control','no-cache')
    $crlf = [string][char]13 + [char]10
    $chunk1 = 'data: {"id":"mock-1","choices":[{"index":0,"delta":{"role":"assistant","content":"I see the image. "}}]}' + $crlf + $crlf
    $chunk2 = 'data: {"id":"mock-1","choices":[{"index":0,"delta":{"content":"Done."}}]}' + $crlf + $crlf
    $done = 'data: [DONE]' + $crlf + $crlf
    $payload = $chunk1 + $chunk2 + $done
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($payload)
    $resp.OutputStream.Write($bytes, 0, $bytes.Length)
  } else {
    $resp.ContentType = 'application/json'
    $json = '{"id":"mock-2","object":"chat.completion","model":"test-model","choices":[{"index":0,"message":{"role":"assistant","content":"ok"},"finish_reason":"stop"}]}'
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
    $resp.OutputStream.Write($bytes, 0, $bytes.Length)
  }
  $resp.OutputStream.Close()
  $resp.Close()
}