using Zenject;
using UnityEngine;

public sealed class GameplayInstaller : MonoInstaller
{
    public override void InstallBindings()
    {
        CombatInstaller.Install(Container);
        PlayerInstaller.Install(Container);
        EnemyInstaller.Install(Container);
        LevelInstaller.Install(Container);
        Container.Bind<GameplayBootStrap>().AsSingle();
        Container.BindInterfacesTo<GameplayEntryPoint>().AsSingle();
        Debug.Log("GameplayInstaller init");
    }
}
