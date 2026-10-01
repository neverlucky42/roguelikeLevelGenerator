public interface IGameStateService 
{
    public string CurrentWorld { get; set; }

    public void SetCurrentWorld(string context);  
}
