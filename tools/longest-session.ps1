# Find longest panel session: ShowAt -> next Hide
$log = "C:\Users\$env:USERNAME\AppData\Local\DynamicBird\Logs\log-20260829.log"
$lines = Get-Content $log | Select-Object -Last 12000
$shows = New-Object System.Collections.ArrayList
$hides = New-Object System.Collections.ArrayList
foreach ($line in $lines) {
  if ($line -match 'ShowAt(left=([-0-9.]+),top=([-0-9.]+)) visible=(w+) cur=(([-0-9.]+),([-0-9.]+))') {
    $ts = $line.Substring(0, 23)
    $null = $shows.Add(@($ts, [double]$matches[1], [double]$matches[2], [double]$matches[4], [double]$matches[5], $line))
  } elseif ($line -match 'Hide() edge=') {
    $ts = $line.Substring(0, 23)
    $null = $hides.Add($ts)
  }
}
"shows=$($shows.Count) hides=$($hides.Count)"
$results = New-Object System.Collections.ArrayList
foreach ($s in $shows) {
  $t1 = [datetime]::ParseExact($s[0], 'yyyy-MM-dd HH:mm:ss.fff', $null)
  foreach ($h in $hides) {
    $t2 = [datetime]::ParseExact($h, 'yyyy-MM-dd HH:mm:ss.fff', $null)
    if ($t2 -gt $t1) {
      $dur = ($t2 - $t1).TotalSeconds
      if ($dur -gt 1) { $null = $results.Add(@($dur, $s[1], $s[2], $s[3], $s[4], $s[5])) }
      break
    }
  }
}
$results | Sort-Object { [double]$_[0] } -Descending | Select-Object -First 6 | ForEach-Object {
  "dur={0:F1}s left={1} top={2} cur=({3},{4}) line={5}" -f $_[0], $_[1], $_[2], $_[3], $_[4], $_[5]
}
