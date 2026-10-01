using System.Collections.Generic;
using UnityEngine;

public sealed class ConfigService : IConfigService
{
    private readonly Dictionary<string, ScriptableObject> configs = new();

    public void Register(string id, ScriptableObject config)
    {
        configs[id] = config;
    }

    public T Get<T>(string id) where T : ScriptableObject
    {
        if (configs.TryGetValue(id, out var config))
        {
            return (T)config;
        }
        throw new KeyNotFoundException($"Config with id '{id}' not found.");
    }
}