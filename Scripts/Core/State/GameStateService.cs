public sealed class GameStateService : IGameStateService
{
    public string CurrentWorld { get; set; }
    
    public void SetCurrentWorld(string context)
    {
        CurrentWorld = context;

    }
}