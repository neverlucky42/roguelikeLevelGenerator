using UnityEngine;

public sealed class LevelSystem : IGameplaySystem
{
    public void Initialize()
    {
        Debug.Log("[System] Level Initialize");
    }
}
