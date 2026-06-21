using System.Diagnostics.CodeAnalysis;

namespace MathEval.Internal;

/// <summary>
/// 线程安全的 LRU 缓存，容量固定，超过容量时淘汰最久未使用的条目
/// </summary>
internal class LruCache<TKey, TValue>(int capacity) where TKey : notnull
{
    private readonly Lock _lock = new();
    private readonly LinkedList<CacheItem> _list = new();
    private readonly Dictionary<TKey, LinkedListNode<CacheItem>> _map = new(capacity);

    private record CacheItem(TKey Key, TValue Value);

    public bool TryGet(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        lock (_lock)
        {
            if (_map.TryGetValue(key, out var node))
            {
                _list.Remove(node);
                _list.AddFirst(node);
                value = node.Value.Value;
                return true;
            }
            value = default;
            return false;
        }
    }

    public void Set(TKey key, TValue value)
    {
        lock (_lock)
        {
            if (_map.TryGetValue(key, out var node))
            {
                _list.Remove(node);
                _list.AddFirst(node);
                node.Value = new CacheItem(key, value);
            }
            else
            {
                if (_map.Count >= capacity)
                {
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

    public TValue GetOrAdd(TKey key, Func<TKey, TValue> factory)
    {
        lock (_lock)
        {
            if (_map.TryGetValue(key, out var node))
            {
                _list.Remove(node);
                _list.AddFirst(node);
                return node.Value.Value;
            }

            if (_map.Count >= capacity)
            {
                var last = _list.Last!;
                _map.Remove(last.Value.Key);
                _list.RemoveLast();
            }

            var value = factory(key);
            var item = new CacheItem(key, value);
            _list.AddFirst(item);
            _map[key] = _list.First!;
            return value;
        }
    }

    public int Count
    {
        get { lock (_lock) return _map.Count; }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _map.Clear();
            _list.Clear();
        }
    }
}
