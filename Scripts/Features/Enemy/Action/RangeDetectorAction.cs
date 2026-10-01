using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "RangeDetector", story: "RangeDetector [target] [detector]", category: "Action", id: "53e579261080ed6b5b2edf69b981dd42")]
public partial class RangeDetectorAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Target;
    [SerializeReference] public BlackboardVariable<RangeDetector> Detector;

    protected override Status OnStart()
    {
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        Target.Value = Detector.Value.UpdateDetector();
        return Target.Value != null ? Status.Success : Status.Failure;
    }

    protected override void OnEnd()
    {
    }
}

