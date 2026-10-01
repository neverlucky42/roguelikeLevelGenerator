using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;
public class HealthsRegistry : MonoBehaviour
{
    private readonly HashSet<IDamagable> targets = new();

    public readonly Dictionary<IDamagable, List<Action<IDamagable, int>>> OnDamagedSubscriptions = new();
    public readonly Dictionary<IDamagable, List<Action<IDamagable>>> OnDeathSubscriptions = new();
    public readonly Dictionary<IDamagable, List<Action<IDamagable>>> OnHealthChangedSubscriptions = new();

    public void Register(IDamagable damagable)
    {
        if (!targets.Add(damagable)) return;

        damagable.OnDamaged += RouteDamage;
        damagable.OnDeath += RouteDeath;
        damagable.OnHealthChanged += RouteHealthChanged;

        OnDamagedSubscriptions[damagable] = new List<Action<IDamagable, int>>();
        OnDeathSubscriptions[damagable] = new List<Action<IDamagable>>();
        OnHealthChangedSubscriptions[damagable] = new List<Action<IDamagable>>();
    }

    public void Unregister(IDamagable damagable)
    {
        targets.Remove(damagable);
    }
    public void SubscribeDamage(IDamagable damagable, Action<IDamagable, int> subscriber)
    {
        if (targets.Contains(damagable))
        {
            OnDamagedSubscriptions[damagable].Add(subscriber);
        }
    }
    public void SubscribeDeath(IDamagable damagable, Action<IDamagable> subscriber)
    {
        if (targets.Contains(damagable))
        {
            OnDeathSubscriptions[damagable].Add(subscriber);
        }
    }
    public void SubscribeHealthChanged(IDamagable damagable, Action<IDamagable> subscriber)
    {
        if (targets.Contains(damagable))
        {
            OnHealthChangedSubscriptions[damagable].Add(subscriber);
        }
    }

    public void RouteDamage(IDamagable damagable, int damage)
    {
        if (targets.Contains(damagable))
        {
            foreach (var subscriber in OnDamagedSubscriptions[damagable])
            {
                subscriber?.Invoke(damagable, damage);
            }
        }
    }

    public void RouteDeath(IDamagable damagable)
    {
        if (targets.Contains(damagable))
        {
            foreach (var subscriber in OnDeathSubscriptions[damagable])
            {
                subscriber?.Invoke(damagable);
            }
        }
    }

    public void RouteHealthChanged(IDamagable damagable)
    {
        if (targets.Contains(damagable))
        {
            foreach (var subscriber in OnHealthChangedSubscriptions[damagable])
            {
                subscriber?.Invoke(damagable);
            }
        }
    }
}