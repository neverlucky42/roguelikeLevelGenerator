using UnityEngine;

public class GameRoot : MonoBehaviour
{
    [SerializeField] private MonoBehaviour[] installers;
    private GameContext context;

    public void Awake()
    {
        context = new GameContext();

    }
}
