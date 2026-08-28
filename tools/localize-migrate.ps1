# DynamicBird XAML localization migration script (fixed: no cross-scope mutable state)
$ErrorActionPreference = 'Stop'
$root = 'D:/bird/timecolors/DynamicBird - 蓝色大肥鱼 - 副本'
$uiDir = Join-Path $root 'src/UI'
$resx = Join-Path $uiDir 'Localization/Strings.resx'
$tsv = Join-Path $root 'tools/localize-keys.tsv'

$existingKeys = @{}
if (Test-Path $resx) {
  $resxText = [IO.File]::ReadAllText($resx)
  [regex]::Matches($resxText, '<data name="([^"]+)"') | ForEach-Object { $existingKeys[$_.Groups[1].Value] = $true }
}

$attrNames = 'Text|Content|Header|ToolTip|Title|Placeholder|Hint|Watermark|Description'
$pattern = '(?<attr>(?:' + $attrNames + '))="(?<val>(?!\{)[^"]*[\u4e00-\u9fff][^"]*)"'

$rows = New-Object System.Collections.Generic.List[string]
$newResxBlocks = New-Object System.Collections.Generic.List[string]
$tabs = [char]9

$files = Get-ChildItem $uiDir -Recurse -Filter *.xaml |
  Where-Object { $_.Name -notin @('AppIcons.xaml','Theme.xaml') }

foreach ($f in $files) {
  $content = [IO.File]::ReadAllText($f.FullName)
  $original = $content
  $className = [regex]::Match($content, 'x:Class="[^"]+\.([^".]+)"').Groups[1].Value
  if (-not $className) { $className = $f.BaseName }

  if ($content -notmatch 'xmlns:loc=') {
    # ^ anchor to root element, avoid matching nested <Window.Resources> etc.
    $content = [regex]::Replace($content, '^<((?:Window|UserControl|Page)[^>]*?)(>)', '$1 xmlns:loc="clr-namespace:DynamicBird.UI.Localization"$2', 1)
  }

  $result = [regex]::Replace($content, $pattern, {
    param($m)
    $val = $m.Groups['val'].Value
    if ($val -match '["<>]') { return $m.Value }
    # 用 rows.Count 作序号，避免跨作用域写不回去的问题
    $key = 'UI_' + $className + '_' + ($rows.Count + 1)
    $escaped = $val.Replace('&','&amp;').Replace('<','&lt;').Replace('>','&gt;')
    $rows.Add($key + $tabs + $escaped + $tabs + $f.Name)
    if (-not $existingKeys.ContainsKey($key)) {
      $nl = [Environment]::NewLine
      $block = '  <data name="' + $key + '" xml:space="preserve">' + $nl + '    <value>' + $escaped + '</value>' + $nl + '  </data>'
      $newResxBlocks.Add($block)
    }
    return $m.Groups['attr'].Value + '="{Binding Item[' + $key + '], Source={x:Static loc:LocalizationManager.Instance}}"'
  })

  if ($result -ne $original) {
    [IO.File]::WriteAllText($f.FullName, $result, [Text.UTF8Encoding]::new($false))
    Write-Output ('MIGRATED ' + $f.Name)
  }
}

[IO.File]::WriteAllLines($tsv, $rows.ToArray(), [Text.UTF8Encoding]::new($false))
Write-Output ('KEYS total=' + $rows.Count)

if ($newResxBlocks.Count -gt 0) {
  $resxText = [IO.File]::ReadAllText($resx)
  $insert = [Environment]::NewLine + ($newResxBlocks.ToArray() -join [Environment]::NewLine) + [Environment]::NewLine
  $resxText = $resxText.Replace('</root>', $insert + '</root>')
  [IO.File]::WriteAllText($resx, $resxText, [Text.UTF8Encoding]::new($false))
  Write-Output ('RESX appended=' + $newResxBlocks.Count)
}