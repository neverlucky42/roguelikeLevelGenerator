using System;
using UnityEngine;

public interface IDamagable
{
    event Action<IDamagable, int> OnDamaged;
    event Action<IDamagable> OnDeath;
    event Action<IDamagable> OnHealthChanged;
    public int MaxHealth {  get; set; }
    public int Damage { get; set; }
    public void TakeDamage(int damage);
}
