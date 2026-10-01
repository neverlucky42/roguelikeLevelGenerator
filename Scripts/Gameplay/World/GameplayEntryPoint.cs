using Zenject;
using UnityEngine;
public sealed class GameplayEntryPoint : IInitializable
{
    private readonly IGameStateService _gameStateService;
    private readonly GameplayBootStrap _gameplayBootStrap;

    public GameplayEntryPoint(IGameStateService gameStateService, GameplayBootStrap gameplayBootStrap)
    {
        _gameStateService = gameStateService;
        _gameplayBootStrap = gameplayBootStrap;
    }
    public void Initialize()
    {
        _gameStateService.CurrentWorld = "MainWorld";
        _gameplayBootStrap.Initialize();
        Debug.Log("[ZENJECT] GameplayEntryPoint init");
    }
}