using System;
using Unity.Behavior;
using UnityEngine;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "TargetLineDetectorCondition", story: "[Target] [LineOfSight]", category: "Conditions", id: "ac6ab54fa151895eefba85143535b32f")]
public partial class TargetLineDetectorCondition : Condition
{
    [SerializeReference] public BlackboardVariable<GameObject> Target;
    [SerializeReference] public BlackboardVariable<LineOfSightDetector> LineOfSight;

    public override void OnStart()
    {

    }

    public override bool IsTrue()
    {
        return LineOfSight.Value.PerformDetection(Target.Value) != null;

    }

    public override void OnEnd()
    {
    }
}

