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
            _state[path[0]] = step;
        }
        
        return FindParentContainer((Dictionary<string, object>)step, path[1..]);
    }
    
    public void Set<T>(T value, params string[] path)
    {
        lock (_state)
        {
            var container = FindParentContainer(_state, path);
            container[path[^1]] = value;
        }
    }

    public IDictionary<string,T> GetMany<T>( params string[] path)
    {
        lock (_state)
        {
            var container = FindParentContainer(_state, path);
            if (container.TryGetValue(path[^1], out var o) && o is Dictionary<string,object> items)
            {
                return items.ToDictionary(x => x.Key, x => (T)x.Value);
            }
            return new Dictionary<string, T>();
        }
    }

    public void Remove(params string[] path)
    {
        lock (_state)
        {
            var container = FindParentContainer(_state, path);
            container.Remove(path[^1]);
        }
    }
    
}