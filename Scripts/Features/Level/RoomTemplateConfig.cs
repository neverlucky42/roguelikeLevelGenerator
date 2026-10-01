using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Levels/Generations/RoomTemplates", fileName = "Room template")]
public sealed class RoomTemplateConfig : ScriptableObject
{
    public RoomTemplate template;
    [Header("Navigation")]
    public NavigationPathMode pathMode;
    
    [Min(0)]
    public int navigationRadius = 1;

    [Range(0f, 1f)]
    public float directnes = .7f;

    [Tooltip("На сколько сильно путь может отклониться от прямой линии")]
    [Min(0)]
    public int maxLateralOffset = 3;

    [Range(0f, 1f)]
    public float turnChance = .35f;

    [Min(0)]
    public int wallMargin = 1;

    [Range(0f, 1f)]
    public float obstacleDensity = .3f;

    [Min(1)]
    public int minObstacleSize = 1;

    [Min(1)]
    public int maxObstacleSize = 1;
}