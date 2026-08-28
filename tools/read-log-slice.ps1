$log = 'C:\Users\时色\AppData\Local\DynamicBird\Logs\log-20260829.log'
$idx = (Select-String -Path $log -Pattern '01:32:06.377' | Select-Object -First 1).LineNumber
if ($idx) {
  Get-Content $log | Select-Object -Skip ($idx - 3) -First 60
} else { 'marker not found' }
