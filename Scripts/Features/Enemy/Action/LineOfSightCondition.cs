using System;
using Unity.Behavior;
using UnityEngine;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "LineOfSight", story: "[Target] [LineDetector]", category: "Conditions", id: "317c47c67f6d29ddb3c9a919057efb66")]
public partial class LineOfSightCondition : Condition
{
    [SerializeReference] public BlackboardVariable<GameObject> Target;
    [SerializeReference] public BlackboardVariable<LineOfSightDetector> LineDetector;

    public override bool IsTrue()
    {
        return LineDetector.Value.PerformDetection(Target.Value) != null;
    }

    public override void OnStart()
    {
    }

    public override void OnEnd()
    {
    }
}
