using System.IO.Hashing;
using System.Text;

namespace M17.Theory;

// ❌ BEFORE: node = hash(key) % N. Add a node (N 4 → 5) and almost every key changes node
//    → a cache cluster loses ~80% of its hits at once; a database must move ~80% of its data.
public static class ModuloRouting
{
    public static int NodeFor(string key, int nodeCount) => (int)(Hash(key) % (ulong)nodeCount);
    internal static ulong Hash(string s) => XxHash64.HashToUInt64(Encoding.UTF8.GetBytes(s));
}

// ✅ AFTER: consistent hashing. Nodes and keys sit on the same ring of hash values;
//    a key belongs to the first node clockwise. Adding a node only takes keys from its neighbours.
//    Virtual nodes: each real node gets many points on the ring → even spread.
public class HashRing(int virtualNodesPerNode = 100)
{
    private readonly SortedList<ulong, string> _ring = new();

    public void AddNode(string node)
    {
        for (var i = 0; i < virtualNodesPerNode; i++)
            _ring[ModuloRouting.Hash($"{node}#{i}")] = node;      // many points per node
    }

    public string NodeFor(string key)
    {
        var h = ModuloRouting.Hash(key);
        var keys = _ring.Keys;
        int lo = 0, hi = keys.Count - 1, found = 0;              // first ring point ≥ h (binary search)
        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            if (keys[mid] >= h) { found = mid; hi = mid - 1; } else lo = mid + 1;
        }
        if (lo == keys.Count) found = 0;                          // past the end → wrap around
        return _ring.Values[found];
    }
}
