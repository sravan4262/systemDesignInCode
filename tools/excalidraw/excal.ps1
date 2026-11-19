param([string]$Layout, [string]$Out)
# Minimal Excalidraw generator: boxes with bound text, arrows bound to boxes with labels, free text.
$script:els = New-Object System.Collections.ArrayList
$script:boxes = @{}
$script:seed = 1000
function NextSeed { $script:seed += 7; return $script:seed }
function Base($id, $type, $x, $y, $w, $h) {
  [ordered]@{ id=$id; type=$type; x=[double]$x; y=[double]$y; width=[double]$w; height=[double]$h; angle=0; strokeColor='#1e1e1e';
    backgroundColor='transparent'; fillStyle='solid'; strokeWidth=2; strokeStyle='solid'; roughness=1; opacity=100;
    groupIds=@(); frameId=$null; roundness=$null; seed=(NextSeed); version=1; versionNonce=(NextSeed); isDeleted=$false;
    boundElements=(New-Object System.Collections.ArrayList); updated=1700000000000; link=$null; locked=$false }
}
function TextEl($id, $x, $y, $w, $h, $text, $container, $size) {
  $t = Base $id 'text' $x $y $w $h
  $t.boundElements = $null
  $t.text=$text; $t.fontSize=$size; $t.fontFamily=1; $t.textAlign='center'; $t.verticalAlign='middle'
  $t.containerId=$container; $t.originalText=$text; $t.lineHeight=1.25; $t.autoResize=$true
  if (-not $container) { $t.textAlign='left'; $t.verticalAlign='top' }
  return $t
}
function Box($id, $x, $y, $w, $h, $text, $bg) {
  $r = Base $id 'rectangle' $x $y $w $h
  $r.backgroundColor = $bg; $r.roundness = @{ type = 3 }
  $lines = $text.Split("`n").Count
  $th = 20 * $lines
  $t = TextEl "$id-t" ($x + 5) ($y + ($h - $th) / 2) ($w - 10) $th $text $id 16
  [void]$r.boundElements.Add(@{ type='text'; id="$id-t" })
  [void]$script:els.Add($r); [void]$script:els.Add($t)
  $script:boxes[$id] = $r
}
function Edge($b, $tx, $ty) {
  # point on the border of box $b along the line from its centre toward (tx,ty)
  $cx = $b.x + $b.width / 2; $cy = $b.y + $b.height / 2
  $dx = $tx - $cx; $dy = $ty - $cy
  if ($dx -eq 0 -and $dy -eq 0) { return @($cx, $cy) }
  $sx = if ($dx -ne 0) { ($b.width / 2) / [math]::Abs($dx) } else { [double]::MaxValue }
  $sy = if ($dy -ne 0) { ($b.height / 2) / [math]::Abs($dy) } else { [double]::MaxValue }
  $s = [math]::Min($sx, $sy)
  return @(($cx + $dx * $s), ($cy + $dy * $s))
}
function Arrow($id, $from, $to, $label, $color, $dashed) {
  $a = $script:boxes[$from]; $b = $script:boxes[$to]
  $acx = $a.x + $a.width / 2; $acy = $a.y + $a.height / 2
  $bcx = $b.x + $b.width / 2; $bcy = $b.y + $b.height / 2
  $p1 = Edge $a $bcx $bcy; $p2 = Edge $b $acx $acy
  $x1 = $p1[0]; $y1 = $p1[1]; $x2 = $p2[0]; $y2 = $p2[1]
  $ar = Base $id 'arrow' $x1 $y1 ([math]::Abs($x2 - $x1)) ([math]::Abs($y2 - $y1))
  $ar.roundness = @{ type = 2 }
  if ($color) { $ar.strokeColor = $color }
  if ($dashed) { $ar.strokeStyle = 'dashed' }
  $ar.points = @( @(0, 0), @(($x2 - $x1), ($y2 - $y1)) )
  $ar.lastCommittedPoint = $null
  $ar.startBinding = @{ elementId=$from; focus=0; gap=4 }
  $ar.endBinding = @{ elementId=$to; focus=0; gap=4 }
  $ar.startArrowhead = $null; $ar.endArrowhead = 'arrow'; $ar.elbowed = $false
  [void]$a.boundElements.Add(@{ type='arrow'; id=$id })
  [void]$b.boundElements.Add(@{ type='arrow'; id=$id })
  [void]$script:els.Add($ar)
  if ($label) {
    $lines = $label.Split("`n").Count
    $w = 8 * ($label.Split("`n") | Measure-Object -Property Length -Maximum).Maximum
    $h = 17 * $lines
    $t = TextEl "$id-t" ((($x1 + $x2) / 2) - $w / 2) ((($y1 + $y2) / 2) - $h / 2) $w $h $label $id 14
    if ($color) { $t.strokeColor = $color }
    [void]$ar.boundElements.Add(@{ type='text'; id="$id-t" })
    [void]$script:els.Add($t)
  }
}
function Note($id, $x, $y, $text, $size, $color) {
  $lines = $text.Split("`n")
  $w = [math]::Round($size * 0.55 * ($lines | Measure-Object -Property Length -Maximum).Maximum)
  $t = TextEl $id $x $y $w ([math]::Round($size * 1.25 * $lines.Count)) $text $null $size
  if ($color) { $t.strokeColor = $color }
  [void]$script:els.Add($t)
}

. $Layout

$doc = [ordered]@{ type='excalidraw'; version=2; source='https://excalidraw.com'; elements=$script:els;
  appState=[ordered]@{ gridSize=20; viewBackgroundColor='#ffffff' }; files=@{} }
$json = $doc | ConvertTo-Json -Depth 30
[IO.File]::WriteAllText($Out, $json, (New-Object System.Text.UTF8Encoding($false)))
"wrote $($script:els.Count) elements to $Out"
