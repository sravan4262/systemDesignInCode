namespace M04.Concurrency;

// LRU cache: fixed capacity; when full, evict the least recently used key. Get and Put are O(1).
// Dictionary = find the node in O(1). LinkedList = keep usage order; move/remove a known node in O(1).
public class LruCache<TKey, TValue>(int capacity) where TKey : notnull
{
    private readonly Dictionary<TKey, LinkedListNode<(TKey Key, TValue Value)>> _map = new();
    private readonly LinkedList<(TKey Key, TValue Value)> _order = new();   // First = most recent, Last = least recent
    private readonly object _lock = new();   // Get ALSO mutates the list, so every call needs the same exclusive lock

    public bool TryGet(TKey key, out TValue value)
    {
        lock (_lock)
        {
            if (!_map.TryGetValue(key, out var node)) { value = default!; return false; }

            _order.Remove(node);       // a read is a "use": move to the front
            _order.AddFirst(node);
            value = node.Value.Value;
            return true;
        }
    }

    public void Put(TKey key, TValue value)
    {
        lock (_lock)
        {
            if (_map.TryGetValue(key, out var existing))
                _order.Remove(existing);                          // update = remove old node, re-add at front
            else if (_map.Count == capacity)
            {
                var lru = _order.Last!;                           // evict from the back
                _order.RemoveLast();
                _map.Remove(lru.Value.Key);                       // keep dictionary and list in sync
            }

            _map[key] = _order.AddFirst((key, value));
        }
    }
}
