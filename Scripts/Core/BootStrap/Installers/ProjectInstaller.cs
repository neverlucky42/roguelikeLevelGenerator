using Zenject;
using UnityEngine;
public class ProjectInstaller : MonoInstaller
{
    public override void InstallBindings()
    {
        Container.Bind<ISceneLoader>().To<SceneLoader>().AsSingle();
        Container.Bind<IConfigService>().To<ConfigService>().AsSingle();
        Container.Bind<IGameStateService>().To<GameStateService>().AsSingle();
        Debug.Log("[ZENJECT] ProjectInstall init");
    }
}
