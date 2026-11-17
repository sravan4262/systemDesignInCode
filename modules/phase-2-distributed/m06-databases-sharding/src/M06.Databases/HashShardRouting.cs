using System.IO.Hashing;
using System.Text;

namespace M06.Databases;

// Hash-based sharding: the shard comes from MATH on the key, not from a per-key lookup table.
// Fits many small, similar keys (e.g. 800M chats). Compare with ShardRouting.cs (directory, per tenant).

// 1) A STABLE hash: same key → same number on every server, after every restart.
//    ❌ Never string.GetHashCode(): randomised per process → same chat lands on different shards.
public static class StableHash
{
    public static uint Of(string key) => XxHash32.HashToUInt32(Encoding.UTF8.GetBytes(key));
}

// 2) ❌ NAIVE: shard = hash % N.  Even spread, no lookup... but change N and ~80% of keys move.
public class NaiveHashShardResolver(IReadOnlyList<string> shards)
{
    public string Resolve(string key) => shards[(int)(StableHash.Of(key) % (uint)shards.Count)];
}

// 3) ✅ FIXED LOGICAL BUCKETS: hash → one of 1,024 buckets (this NEVER changes),
//    then a small bucket → shard table (1,024 rows, not 800M). Adding a shard = reassign SOME buckets;
//    only keys in those buckets move. (Redis Cluster: 16,384 slots. Cosmos DB: logical → physical partitions.)
public class BucketShardResolver
{
    public const int BucketCount = 1024;               // fixed forever: pick it much bigger than your max shard count
    private readonly string[] _bucketToShard = new string[BucketCount];
    private readonly Lock _writeLock = new();

    public BucketShardResolver(IReadOnlyList<string> shards)
    {
        for (var b = 0; b < BucketCount; b++)
            _bucketToShard[b] = shards[b % shards.Count];  // spread buckets evenly at the start
    }

    public static int BucketOf(string key) => (int)(StableHash.Of(key) % BucketCount);

    // Hot path: hash + one array read. No database, no cache. (Reading one array slot is atomic.)
    public string Resolve(string key) => _bucketToShard[BucketOf(key)];

    // Plan for adding a shard: take an equal share of buckets from every existing shard.
    // Returns the moves; the DATA copy happens before you apply them (Session 9: rebalancing).
    public List<(int Bucket, string From, string To)> PlanAddShard(string newShard)
    {
        var shardCountAfter = _bucketToShard.Distinct().Count() + 1;
        var target = BucketCount / shardCountAfter;        // buckets the new shard should own

        return _bucketToShard
            .Select((shard, bucket) => (Bucket: bucket, From: shard))
            .GroupBy(x => x.From)
            .SelectMany(g => g.Take(target / (shardCountAfter - 1)))   // a fair slice from each shard
            .Select(x => (x.Bucket, x.From, newShard))
            .ToList();
    }

    // Apply ONE move after its data has been copied (and writes to it paused or double-written).
    public void MoveBucket(int bucket, string toShard)
    {
        lock (_writeLock) _bucketToShard[bucket] = toShard;
    }
}

// 4) MEASURE: what fraction of keys change shard when going from 4 to 5 shards?
//    Measured (100,000 keys): naive hash % N moved 79.8%; fixed buckets moved 19.8% (the minimum: 1/5).
public static class HashShardingExperiment
{
    public static (double NaiveMoved, double BucketsMoved) FourToFive(int keyCount = 100_000)
    {
        var keys = Enumerable.Range(0, keyCount).Select(i => $"chat-{i}").ToList();
        string[] four = ["s0", "s1", "s2", "s3"];
        string[] five = ["s0", "s1", "s2", "s3", "s4"];

        // Naive: just change N.
        var naive4 = new NaiveHashShardResolver(four);
        var naive5 = new NaiveHashShardResolver(five);
        var naiveMoved = keys.Count(k => naive4.Resolve(k) != naive5.Resolve(k));

        // Buckets: same 4-shard start, then move only the planned buckets to s4.
        var buckets = new BucketShardResolver(four);
        var before = keys.ToDictionary(k => k, buckets.Resolve);
        foreach (var (bucket, _, to) in buckets.PlanAddShard("s4"))
            buckets.MoveBucket(bucket, to);
        var bucketsMoved = keys.Count(k => before[k] != buckets.Resolve(k));

        return ((double)naiveMoved / keyCount, (double)bucketsMoved / keyCount);
    }
}
