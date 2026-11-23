# M25: hybrid router (customer-support assistant). Rows: A = customer (3 lanes after the router), B = human agent.
$edge = '#ffc9c9'; $svc = '#a5d8ff'; $data = '#b2f2bb'; $async = '#ffec99'; $ai = '#d0bfff'; $grey = '#e9ecef'
$w = '#1971c2'; $r = '#2f9e44'; $h = '#e8590c'; $note = '#495057'

function Frame($id, $x, $y, $wd, $ht) {
  $f = Base $id 'rectangle' $x $y $wd $ht
  $f.strokeStyle = 'dashed'; $f.strokeColor = '#868e96'; $f.strokeWidth = 1; $f.roundness = @{ type = 3 }
  [void]$script:els.Add($f)
}
function Col($n) { return 40 + 340 * $n }
$BW = 230; $H = 75

Note 'title' 0 -70 "M25: hybrid router - customer -> APIM -> router -> FIXED FLOW (actions) / AI + RAG (questions) / HUMAN (hand-off)" 28 $null
Note 'legend' 0 -25 "Blue = action (write) lane   Green = question (read) lane   Orange = hand-off   Dashed = async   Purple = AI model call" 16 $note

# =================== ROW A: CUSTOMER ===================
$T = 0
Frame 'fA' 0 $T 3060 760
Note 'At' 20 ($T + 12) "A. CUSTOMER (200k conversations/day; 30% actions, 50% questions, 15% greetings, 5% hand-off)" 24 $w
$l1 = $T + 100; $l2 = $T + 320; $l3 = $T + 540   # lanes: action, question, hand-off
$top = $l1; $tall = $l3 + $H - $l1
Note 'Awl' (Col 5) ($T + 62) "ACTION LANE: FIXED FLOW (code acts, model only classifies)" 18 $w
Note 'Arl' (Col 5) ($T + 282) "QUESTION LANE: AI + RAG (never calls write APIs)" 18 $r
Note 'Ahl' (Col 5) ($T + 502) "HAND-OFF LANE" 18 $h

Box 'Au' (Col 0) ($T + 320) $BW 80 "Customer`nchat in web / app`n(logged in)" $edge
Box 'Ag' (Col 1) $top $BW $tall "APIM`n`nEntra External ID:`ncustomerId in token`n`nrate limits" $edge
Box 'Acs' (Col 2) $top $BW $tall "Conversation Service`n`nstate: Bot ->`nWaitingForHuman ->`nWithHuman -> Closed`n`nstreams replies (SSE)`n`nCosmos DB" $svc
Box 'Agr' (Col 3) $top $BW $tall "Input guardrails`n`nPII redaction`nmoderation`n`n(output guardrails`non the way back)" $svc
Box 'Art' (Col 4) $top $BW $tall "ROUTER`n`n1 rules: hi/thanks ->`ncanned; 'human' ->`nhand-off`n`n2 small model:`n{ intent, entities,`nconfidence }`n`n3 dispatch by intent`n+ confidence" $ai
Arrow 'Aa1' 'Au' 'Ag' "HTTPS / SSE" $null $false $true
Arrow 'Aa2' 'Ag' 'Acs' "" $null $false $true
Arrow 'Aa3' 'Acs' 'Agr' "" $null $false $true
Arrow 'Aa4' 'Agr' 'Art' "redacted text" $null $false $true

# action lane
Box 'Afl' (Col 5) $l1 $BW $H "Flow engine (code)`nfill slots: which order?`n-> action_proposal" $svc
Box 'Acf' (Col 6) $l1 $BW $H "Customer CONFIRMS`nPOST /actions/{id}/`nconfirm (idempotency key)" $edge
Box 'Aapi' (Col 7) $l1 $BW $H "Order / Refund API`nowner check (token)`n<= 50 auto refund" $data
Box 'Apend' (Col 8) $l1 $BW $H "> 50: pending approval`n-> row B (human)" $async
Arrow 'Al1' 'Art' 'Afl' "refund, 0.97" $w $false $true
Arrow 'Al2' 'Afl' 'Acf' "propose" $w
Arrow 'Al3' 'Acf' 'Aapi' "confirm" $w
Arrow 'Al4' 'Aapi' 'Apend' "over limit" $w $true

# question lane
Box 'Asc' (Col 5) $l2 $BW $H "Semantic cache (Redis)`nscope = help-centre`nversion (no personal)" $data
Box 'Ase' (Col 6) $l2 $BW $H "Hybrid search`nhelp-centre articles`n-> rerank -> top 5" $data
Box 'Aam' (Col 7) $l2 $BW $H "Answer model`nonly from sources,`ncite, or 'I don't know'" $ai
Box 'Aog' (Col 8) $l2 $BW $H "Output guardrails`nPII, exfiltration`n-> stream to customer" $svc
Arrow 'Aq1' 'Art' 'Asc' "policy_question, 0.9" $r $false $true
Arrow 'Aq2' 'Asc' 'Ase' "miss" $r
Arrow 'Aq3' 'Ase' 'Aam' "top 5" $r
Arrow 'Aq4' 'Aam' 'Aog' "answer" $r

# hand-off lane
Box 'Ahs' (Col 5) $l3 $BW $H "Hand-off service`nsummary (small model)`n+ priority" $ai
Box 'Ahq' (Col 6) $l3 $BW $H "Queue (Service Bus)`n+ priority view (SQL)" $async
Box 'Ahc' (Col 7) $l3 $BW $H "state = WaitingForHuman`nbot stops; customer`nsees queue position" $grey
Arrow 'Ah1' 'Art' 'Ahs' "asked / conf < 0.6 /`nsensitive / 2 fails" $h $false $true
Arrow 'Ah2' 'Ahs' 'Ahq' "enqueue" $h $true
Arrow 'Ah3' 'Ahq' 'Ahc' "" $h $true

Note 'Asum' (Col 8) ($l3 - 20) ("Model CLASSIFIES, code ACTS:`nno free-form model decides`nan amount or an order.`nAI lane has no write APIs.") 16 $note

# =================== ROW B: HUMAN AGENT ===================
$T = 840
Frame 'fB' 0 $T 3060 560
Note 'Bt' 20 ($T + 12) "B. HUMAN SUPPORT AGENT (~155 agents; 10k hand-offs/day x ~6 min)" 24 $h
$w1 = $T + 100; $w2 = $T + 210; $w3 = $T + 320; $r1 = $T + 450
Note 'Bwl' (Col 3) ($T + 62) "WRITE PATH" 18 $w
Note 'Brl' (Col 3) ($T + 412) "READ PATH" 18 $r

Box 'Bu' (Col 0) ($T + 250) $BW 80 "Support agent`nagent console`n(web + SignalR)" $edge
Box 'Bg' (Col 1) $w1 $BW ($r1 + $H - $w1) "APIM`n`nEntra ID:`nagent role required" $edge
Box 'Bcon' (Col 2) $w1 $BW ($r1 + $H - $w1) "Agent console API`n`nreal-time updates`n(SignalR)" $svc
Arrow 'Ba1' 'Bu' 'Bg' "HTTPS / WS" $null $false $true
Arrow 'Ba2' 'Bg' 'Bcon' "" $null $false $true

Box 'Bcl' (Col 3) $w1 $BW $H "Claim conversation`nUPDATE ... WHERE state`n= WaitingForHuman" $svc
Box 'Bcl2' (Col 4) $w1 $BW $H "state = WithHuman`n(2nd agent -> 409)" $data
Box 'Brp' (Col 3) $w2 $BW $H "Reply to customer" $svc
Box 'Brp2' (Col 4) $w2 $BW $H "Conversation Service`n-> push to customer" $svc
Box 'Bap' (Col 3) $w3 $BW $H "Approve / deny refund`n(+ release to bot)" $svc
Box 'Bap2' (Col 4) $w3 $BW $H "Refund API: delete pending`n+ refund in ONE txn" $data
Arrow 'Bw1' 'Bcon' 'Bcl' "claim" $w $false $true
Arrow 'Bw2' 'Bcl' 'Bcl2' "conditional update" $w
Arrow 'Bw3' 'Bcon' 'Brp' "reply" $w $false $true
Arrow 'Bw4' 'Brp' 'Brp2' "" $w
Arrow 'Bw5' 'Bcon' 'Bap' "approve" $w $false $true
Arrow 'Bw6' 'Bap' 'Bap2' "approvalId" $w

Box 'Bq' (Col 3) $r1 $BW $H "Queue: priority +`nwaiting time" $async
Box 'Bsm' (Col 4) $r1 $BW $H "Summary + full history`n(Cosmos)" $data
Box 'Bor' (Col 5) $r1 $BW $H "Customer's orders`n(read-only)" $data
Arrow 'Br1' 'Bcon' 'Bq' "GET /agent/queue" $r $false $true
Arrow 'Br2' 'Bq' 'Bsm' "open" $r
Arrow 'Br3' 'Bsm' 'Bor' "context" $r

Note 'Bsum' (Col 6) ($T + 110) ("WRITE touches: console API, Conversation Service`n(state machine, push), Refund API (approval txn).`n`nREAD touches: queue, Cosmos (summary + history),`nOrder API (read-only).`n`nClaim = conditional update, so only one agent`ngets each customer. The bot is silent while`nstate = WithHuman.") 16 $note
