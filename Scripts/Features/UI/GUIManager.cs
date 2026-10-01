using UnityEngine;
using TMPro;
public class GUIManager : MonoBehaviour
{
    public TextMeshProUGUI SpeedT;

    void Start()
    {
        
    }

    
    void Update()
    {
        
    }
    public void SetSpeed(float speed)
    {
        SpeedT.text = speed.ToString();
    }
}
