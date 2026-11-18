using M17.Theory;

// Measure: how many of 100,000 keys move when we go from 4 nodes to 5?
var keys = Enumerable.Range(0, 100_000).Select(i => $"user:{i}").ToList();

// Modulo
var moduloMoved = keys.Count(k => ModuloRouting.NodeFor(k, 4) != ModuloRouting.NodeFor(k, 5));

// Consistent hashing
var ring = new HashRing();
foreach (var n in new[] { "redis-1", "redis-2", "redis-3", "redis-4" }) ring.AddNode(n);
var before = keys.ToDictionary(k => k, ring.NodeFor);
ring.AddNode("redis-5");
var ringMoved = keys.Count(k => ring.NodeFor(k) != before[k]);
var perNode = keys.GroupBy(ring.NodeFor).OrderBy(g => g.Key).Select(g => $"{g.Key}={g.Count()}");

Console.WriteLine($"modulo 4→5:      {moduloMoved * 100.0 / keys.Count:F1}% of keys moved");
Console.WriteLine($"hash ring 4→5:   {ringMoved * 100.0 / keys.Count:F1}% of keys moved (ideal = 1/5 = 20%)");
Console.WriteLine($"spread on ring:  {string.Join("  ", perNode)}");
