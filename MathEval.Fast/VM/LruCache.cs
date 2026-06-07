namespace MathEval.Fast.VM;

internal class LruCache<TKey, TValue> where TKey : notnull {
    private readonly int _capacity;
    private readonly Dictionary<TKey, LinkedListNode<CacheItem>> _map;
    private readonly LinkedList<CacheItem> _list;
    private readonly object _lock = new();

    private record CacheItem(TKey Key, TValue Value);

    public LruCache(int capacity) {
        _capacity = capacity;
        _map = new Dictionary<TKey, LinkedListNode<CacheItem>>(capacity);
        _list = new LinkedList<CacheItem>();
    }

    public bool TryGet(TKey key, out TValue? value) {
        lock (_lock) {
            if (_map.TryGetValue(key, out var node)) {
                _list.Remove(node);
                _list.AddFirst(node);
                value = node.Value.Value;
                return true;
            }
            value = default;
            return false;
        }
    }

    public void Set(TKey key, TValue value) {
        lock (_lock) {
            if (_map.TryGetValue(key, out var node)) {
                _list.Remove(node);
                _list.AddFirst(node);
                node.Value = new CacheItem(key, value);
            } else {
                if (_map.Count >= _capacity) {
                    var last = _list.Last!;
                    _map.Remove(last.Value.Key);
                    _list.RemoveLast();
                }
                var item = new CacheItem(key, value);
                _list.AddFirst(item);
                _map[key] = _list.First!;
            }
        }
    }

    public void Clear() {
        lock (_lock) {
            _map.Clear();
            _list.Clear();
        }
    }
}
