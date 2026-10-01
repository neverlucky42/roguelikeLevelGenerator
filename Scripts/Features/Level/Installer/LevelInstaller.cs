using Zenject;

public sealed class LevelInstaller : Installer<LevelInstaller>
{
    public override void InstallBindings()
    {
        Container.Bind<IGameplaySystem>().To<LevelSystem>().AsSingle();
    }
}
