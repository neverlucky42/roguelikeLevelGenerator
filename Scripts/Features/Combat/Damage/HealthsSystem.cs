using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;
public class HealthsSystem : MonoBehaviour, IDamagable
{
    [SerializeField] private int maxHealth;
    public event Action<IDamagable, int> OnDamaged;
    public event Action<IDamagable> OnDeath;
    public event Action<IDamagable> OnHealthChanged;
    public int MaxHealth 
    {
        get => maxHealth;
        set => maxHealth = value;
    }
    public int Damage { get; set; }

    public void TakeDamage(int damage)
    {
        Damage += damage;
        OnDamaged?.Invoke(this, damage);
        if (Damage >= MaxHealth)
        {
            Die();
        }
    }
    private void Die()
    {
        OnDeath?.Invoke(this);
    }
}

//    public List<Image> Images = new List<Image>();


//    void Start()
//    {

//    }


//    void Update()
//    {

//    }

//    void updateHearts()
//    {
//        for (int i = 0; i < Images.Count; i++)
//        {
//            int healthValue = (i + 1) * 2;
//            if (Health >= healthValue)
//            {
//                Images[i].fillAmount = 1;

//            }
//            else if (Health >= healthValue - 1)
//            {
//                Images[i].fillAmount = 0.5f;
//            }
//            else
//            {
//                Images[i].fillAmount = 0;
//            }
//        }
//    }
//    void takeDamage(int damage)
//    {
//        if (Health > 0)
//        {
//            Health -= damage;
//            updateHearts();
//        }
//    }
//    void takeHeal(int heal)
//    {
//        if (Health < maxHealth)
//        {
//            Health += heal;
//            updateHearts();
//        }

//    }
//    [ContextMenu("damage 1")]
//    void debugDamage()
//    {
//        takeDamage(1);
//    }
//    [ContextMenu("heal 1")]
//    void debugHeal()
//    {
//        takeHeal(1);
//    }

//    public void TakeDamage(float damage)
//    {
//        takeDamage((int)damage);
//    }
