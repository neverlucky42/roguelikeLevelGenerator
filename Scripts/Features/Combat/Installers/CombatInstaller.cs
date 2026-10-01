using Zenject;
using UnityEngine;
public sealed class CombatInstaller : Installer<CombatInstaller>
{
    public override void InstallBindings()
    {
        Container.Bind<HealthsRegistry>().AsSingle();
        Debug.Log("[ZENJECT] CombatInstall init");
    }
}