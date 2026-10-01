using System.Threading.Tasks;
public interface ISceneLoader
{
    Task LoadSceneAsync(string sceneName);

}