using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public AudioSource m_AudioSource;
    public List<AudioClip> StepClips;
    public List<AudioClip> PunchClips;


    void Start()
    {
        
    }

    
    void Update()
    {
        
    }

    public void Step()
    {
        m_AudioSource.PlayOneShot(StepClips[Random.Range(0, StepClips.Count)]);
    }
    public void Punch()
    {
        m_AudioSource.PlayOneShot(PunchClips[Random.Range(0, PunchClips.Count)]);

    }
}
