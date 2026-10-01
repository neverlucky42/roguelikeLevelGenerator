using System.Collections.Generic;
using UnityEngine;
public sealed class GameplayBootStrap
{
    private readonly List<IGameplaySystem> systems;
    public GameplayBootStrap(List<IGameplaySystem> systems)
    {
        this.systems = systems;
    }
    
    public void Initialize()
    {
        Debug.Log("[BOOTSTRAP] system initialize");
        foreach (var system in systems)
        {
            system.Initialize();
        }
    }
}