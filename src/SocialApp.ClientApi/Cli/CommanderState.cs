namespace SocialApp.ClientApi.Cli;

public sealed class CommanderState
{
    private Dictionary<string, object> _state = new();
    private Dictionary<string, object> FindParentContainer(Dictionary<string, object> current, string[] path)
    {
        if (path.Length == 1)
            return current;

        if (!current.TryGetValue(path[0], out var step))
        {
            step = new Dictionary<string, object>();
            current[path[0]] = step;
        }
        
        return FindParentContainer((Dictionary<string, object>)step, path[1..]);
    }
    
    public void Set<T>(T value, params string[] path)
    {
        if(path == null || path.Length == 0)
            throw new InvalidOperationException("Path cannot be null or empty.");
        
        lock (_state)
        {
            var container = FindParentContainer(_state, path);
            container[path[^1]] = value;
        }
    }
    
    public T? Get<T>(params string[] path)
    {
        if(path == null || path.Length == 0)
            throw new InvalidOperationException("Path cannot be null or empty.");
        
        lock (_state)
        {
            var container = FindParentContainer(_state, path);
            if (!container.TryGetValue(path[^1], out var value))
                return default;
            if (value is T result)
                return result;
            throw new InvalidOperationException($"Value at path {string.Join("/",path)} is not {typeof(T)}");
        }
    }

    public IDictionary<string,T> GetMany<T>( params string[] path)
    {
        if(path == null || path.Length == 0)
            throw new InvalidOperationException("Path cannot be null or empty.");
        
        lock (_state)
        {
            var container = FindParentContainer(_state, path);
            if (container.TryGetValue(path[^1], out var o) && o is Dictionary<string,object> items)
            {
                var result = new Dictionary<string, T>(items.Count);
                foreach (var item in items)
                {
                    if(item.Value is T casted) 
                        result[item.Key] = casted;
                    else
                        throw new InvalidOperationException($"Value at path {string.Join("/",path)}/{item.Key} is not {typeof(T)}");
                }
                return result;
            }
            return new Dictionary<string, T>();
        }
    }

    public void Remove(params string[] path)
    {
        if(path == null || path.Length == 0)
            throw new InvalidOperationException("Path cannot be null or empty.");
        
        lock (_state)
        {
            var container = FindParentContainer(_state, path);
            container.Remove(path[^1]);
        }
    }
}