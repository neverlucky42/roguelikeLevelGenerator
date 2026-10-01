using Zenject;
using UnityEngine;
public sealed class PlayerInstaller : Installer<PlayerInstaller>
{
    public override void InstallBindings()
    {
        //TODO
        Debug.Log("[ZENJECT] PlayerInstall init");
    }
}