$edge = '#ffc9c9'; $svc = '#a5d8ff'; $data = '#b2f2bb'; $async = '#ffec99'; $grey = '#e9ecef'
$w = '#1971c2'; $r = '#2f9e44'; $red = '#e03131'; $note = '#495057'

function Frame($id, $x, $y, $wd, $ht) {
  $f = Base $id 'rectangle' $x $y $wd $ht
  $f.strokeStyle = 'dashed'; $f.strokeColor = '#868e96'; $f.strokeWidth = 1; $f.roundness = @{ type = 3 }
  [void]$script:els.Add($f)
}
function Col($n) { return 40 + 310 * $n }
$BW = 230; $H = 70

Note 'title' 0 -70 "Case 3: Twitter - user -> gateway -> WRITE path vs. READ path" 28 $null
Note 'legend' 0 -25 "Blue = write path   Green = read path   Dashed = async   Red = skipped" 16 $note

# =================== ROW A: REGULAR USER ===================
$T = 0
Frame 'fA' 0 $T 2640 820
Note 'At' 20 ($T + 12) "A. REGULAR USER (Alice, 200 followers)" 24 $w
$w1 = $T + 90; $w2 = $T + 200; $w3 = $T + 310; $R1 = $T + 470; $R2 = $T + 580
Note 'Awl' (Col 2) ($T + 55) "WRITE PATH" 18 $w
Note 'Arl' (Col 2) ($T + 435) "READ PATH" 18 $r

Box 'Au'  (Col 0) ($T + 320) $BW 80 "User (Alice / Bob)`nweb / mobile" $edge
Box 'Ab'  (Col 0) ($T + 150) $BW $H "Blob storage (Azure Blob)`n(media upload, SAS)" $data
Box 'Acdn' (Col 0) ($T + 500) $BW $H "CDN`n(media reads)" $edge
Box 'Ag'  (Col 1) ($T + 320) $BW 80 "API Gateway / LB`nauth, rate limit,`nroute by method" $edge
Arrow 'Aa0' 'Au' 'Ab' "upload" $w $false
Arrow 'Aa00' 'Au' 'Acdn' "load" $r $false
Arrow 'Aa1' 'Au' 'Ag' "HTTPS" $null $false

# write services
Box 'Ats' (Col 2) $w1 $BW $H "Tweet Service`nPOST /tweets" $svc
Box 'Ags' (Col 2) $w2 $BW $H "Follow Service`nPOST|DELETE /follows" $svc
Box 'Als' (Col 2) $w3 $BW $H "Like Service`nPOST|DELETE /likes" $svc
Box 'Atd' (Col 3) $w1 $BW $H "Tweets store (Cassandra)`ntweets + user_tweets`n+ outbox" $data
Box 'Agd' (Col 3) $w2 $BW $H "Follow store (SQL,`nsharded by user_id)`nfollowing + outbox" $data
Box 'Ald' (Col 3) $w3 $BW $H "Likes store (Cassandra)`ninsert-if-new row" $data
Box 'Ak'  (Col 4) $w1 $BW 180 "Kafka (event log)`ntweet-created`nfollow-events" $async
Box 'Alc' (Col 4) $w3 $BW $H "Like counter (Redis)`nINCR if new row" $data
Box 'Afo' (Col 5) $w1 $BW $H "Fan-out worker`nreads followers (SQL)`n< 1M: PUSH" $async
Box 'Agw' (Col 5) $w2 $BW $H "Follow worker`nfollowers edge + backfill" $async
Box 'Atl' (Col 6) $w1 $BW 180 "Timeline cache (Redis)`nLPUSH to 200 followers`n+ backfill / LREM`non follow / unfollow" $data
Arrow 'Aw1' 'Ag' 'Ats' "post" $w $false
Arrow 'Aw2' 'Ag' 'Ags' "follow" $w $false
Arrow 'Aw3' 'Ag' 'Als' "like" $w $false
Arrow 'Aw4' 'Ats' 'Atd' "write" $w $false
Arrow 'Aw5' 'Ags' 'Agd' "write" $w $false
Arrow 'Aw6' 'Als' 'Ald' "insert" $w $false
Arrow 'Aw7' 'Atd' 'Ak' "outbox" $w $true
Arrow 'Aw8' 'Agd' 'Ak' "outbox" $w $true
Arrow 'Aw9' 'Ald' 'Alc' "+1" $w $false
Arrow 'Aw10' 'Ak' 'Afo' "" $w $true
Arrow 'Aw11' 'Ak' 'Agw' "" $w $true
Arrow 'Aw12' 'Afo' 'Atl' "push id" $w $true
Arrow 'Aw13' 'Agw' 'Atl' "backfill" $w $true

# read services
Box 'Ats2' (Col 2) $R1 $BW 180 "Timeline Service`nGET /home`nGET /users/{id}/tweets" $svc
Box 'Atl2' (Col 3) $R1 $BW $H "Timeline cache (Redis)`nhome: 20 ids" $data
Box 'Aut2' (Col 3) $R2 $BW $H "user_tweets (Cassandra)`nprofile: 20 ids" $data
Box 'Atc2' (Col 4) $R1 $BW 180 "Tweet cache (Redis)`n+ like counter`n(hydrate 20 tweets`n+ counts)" $data
Box 'Atd2' (Col 5) $R1 $BW 180 "Tweets store (Cassandra)`n(only on miss)" $data
Arrow 'Ar1' 'Ag' 'Ats2' "read" $r $false
Arrow 'Ar2' 'Ats2' 'Atl2' "home" $r $false
Arrow 'Ar3' 'Ats2' 'Aut2' "profile" $r $false
Arrow 'Ar4' 'Ats2' 'Atc2' "hydrate" $r $false
Arrow 'Ar5' 'Atc2' 'Atd2' "miss" $r $false

Note 'Asum' (Col 6) ($T + 470) ("WRITE touches: Tweet / Follow / Like Service,`nTweets / Follow / Likes store, Kafka,`nfan-out + follow workers, Timeline cache, like counter.`n`nREAD touches: Timeline Service, Timeline cache,`nuser_tweets, Tweet cache, like counter;`nTweets store only on a cache miss.") 16 $note

# =================== ROW B: CELEBRITY ===================
$T = 900
Frame 'fB' 0 $T 2640 960
Note 'Bt' 20 ($T + 12) "B. CELEBRITY (50M followers; Bob follows 5 celebrities)" 24 $red
$w1 = $T + 90; $w2 = $T + 200; $w3 = $T + 310; $w4 = $T + 420
$R1 = $T + 580; $R2 = $T + 690; $R3 = $T + 800
Note 'Bwl' (Col 2) ($T + 55) "WRITE PATH" 18 $w
Note 'Brl' (Col 2) ($T + 545) "READ PATH" 18 $r

Box 'Bu'  (Col 0) ($T + 400) $BW 80 "User (celebrity /`nfollowers)" $edge
Box 'Bb'  (Col 0) ($T + 230) $BW $H "Blob storage (Azure Blob)`n(media upload, SAS)" $data
Box 'Bcdn' (Col 0) ($T + 580) $BW $H "CDN`n(absorbs media spike)" $edge
Box 'Bg'  (Col 1) ($T + 400) $BW 80 "API Gateway / LB`nauth, rate limit,`nroute by method" $edge
Arrow 'Ba0' 'Bu' 'Bb' "upload" $w $false
Arrow 'Ba00' 'Bu' 'Bcdn' "load" $r $false
Arrow 'Ba1' 'Bu' 'Bg' "HTTPS" $null $false

# write services
Box 'Bts' (Col 2) $w1 $BW $H "Tweet Service`nPOST /tweets" $svc
Box 'Bgs' (Col 2) $w2 $BW $H "Follow Service`nPOST|DELETE /follows" $svc
Box 'Bls' (Col 2) $w3 $BW $H "Like Service`nPOST /likes (viral)" $svc
Box 'Bsc' (Col 2) $w4 $BW $H "Split counter (Redis)`nINCR likes:{t}:{0..15}" $data
Box 'Btd' (Col 3) $w1 $BW $H "Tweets store (Cassandra)`ntweets + user_tweets`n+ outbox" $data
Box 'Bgd' (Col 3) $w2 $BW $H "Follow store (SQL,`nsharded by user_id)`nfollowing + outbox" $data
Box 'Bk'  (Col 4) $w1 $BW 290 "Kafka (event log)`ntweet-created`nfollow-events`nlike-events" $async
Box 'Bfo' (Col 5) $w1 $BW $H "Fan-out worker`n>= 1M followers: SKIP" $async
Box 'Bgw' (Col 5) $w2 $BW $H "Follow worker`nNO backfill" $async
Box 'Bag' (Col 5) $w3 $BW $H "Like aggregator`nbatch every 1 s" $async
Box 'Brc' (Col 6) $w1 $BW $H "Celebrity recent cache`n(Redis sorted set)`nZADD last 100 ids" $data
Box 'Bx'  (Col 7) $w2 $BW $H "50M follower timelines`nNOT written" $grey
Box 'Bcs' (Col 6) $w2 $BW $H "celebs:bob (Redis set)`n+ follower count (SQL)" $data
Box 'Bld' (Col 6) $w3 $BW $H "Likes store (Cassandra)`nbatch insert-if-new" $data
Arrow 'Bw1' 'Bg' 'Bts' "post" $w $false
Arrow 'Bw2' 'Bg' 'Bgs' "follow" $w $false
Arrow 'Bw3' 'Bg' 'Bls' "like" $w $false
Arrow 'Bw4' 'Bts' 'Btd' "write" $w $false
Arrow 'Bw5' 'Bgs' 'Bgd' "write" $w $false
Arrow 'Bw6' 'Bls' 'Bsc' "+1 random shard" $w $false
Arrow 'Bw7' 'Btd' 'Bk' "outbox" $w $true
Arrow 'Bw8' 'Bgd' 'Bk' "outbox" $w $true
Arrow 'Bw9' 'Bls' 'Bk' "event -> 202" $w $true
Arrow 'Bw10' 'Bk' 'Bfo' "" $w $true
Arrow 'Bw11' 'Bk' 'Bgw' "" $w $true
Arrow 'Bw12' 'Bk' 'Bag' "" $w $true
Arrow 'Bw13' 'Bfo' 'Brc' "add id" $w $true
Arrow 'Bw14' 'Bfo' 'Bx' "" $red $true
Arrow 'Bw15' 'Bgw' 'Bcs' "SADD" $w $true
Arrow 'Bw16' 'Bag' 'Bld' "batch" $w $true
$script:boxes['Bx'].strokeColor = $red; $script:boxes['Bx'].strokeStyle = 'dashed'

# read services
Box 'Bts2' (Col 2) $R1 $BW 290 "Timeline Service`nGET /home`n(push + pull merge)`nGET /users/{celeb}/tweets`n+ in-process cache" $svc
Box 'Btl2' (Col 3) ($R1 - 10) $BW $H "Timeline cache (Redis)`npushed ids" $data
Box 'Bcs2' (Col 3) ($R1 + 65) $BW $H "celebs:bob (Redis set)`n-> 5 celebs" $data
Box 'Brc2' (Col 3) ($R1 + 160) $BW $H "Celebrity recent (Redis)`nhome merge + profile" $data
Box 'Btc2' (Col 4) $R1 $BW 290 "Tweet cache (Redis)`n+ sum of 16`ncounter shards`n(hydrate top 20)" $data
Box 'Btd2' (Col 5) $R1 $BW 290 "Tweets store (Cassandra)`n(only on miss;`ncelebrity reads`nnever get here)" $data
Arrow 'Br1' 'Bg' 'Bts2' "read" $r $false
Arrow 'Br2' 'Bts2' 'Btl2' "a" $r $false
Arrow 'Br3' 'Bts2' 'Bcs2' "b" $r $false
Arrow 'Br4' 'Bts2' 'Brc2' "c" $r $false
Arrow 'Br5' 'Bts2' 'Btc2' "hydrate" $r $false
Arrow 'Br6' 'Btc2' 'Btd2' "miss" $r $false

Note 'Bsum' (Col 6) ($T + 580) ("WRITE touches: same services as A, but the fan-out writes ONE`ncelebrity list (not 50M timelines), follow does no backfill,`nand likes go split counter + Kafka + batch aggregator.`n`nREAD touches: Timeline Service (merges a + c in parallel),`nTimeline cache, celebs set, celebrity recent cache,`nin-process cache + Tweet cache, counter shards.`nTweets store only on a miss.") 16 $note
