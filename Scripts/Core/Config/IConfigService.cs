using UnityEngine;
public interface IConfigService
{
    T Get<T>(string id) where T : ScriptableObject;

}
