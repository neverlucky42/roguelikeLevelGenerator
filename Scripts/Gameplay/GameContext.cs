
public sealed class GameContext
{
    public HealthsRegistry HealthsRegistry { get; private set; }

    public GameContext()
    {
        HealthsRegistry = new HealthsRegistry();
    }

}