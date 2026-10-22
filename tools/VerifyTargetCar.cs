if (!UnityEditor.EditorApplication.isPlaying) throw new System.InvalidOperationException("Run in Play Mode only.");
var source = UnityEngine.Object.FindFirstObjectByType<GodOfSpeed.Racing.PhysicsSplineCarDriver>();
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
var type = source.GetType();
var oldMode = UnityEngine.Physics.simulationMode;
var oldDelta = UnityEngine.Time.fixedDeltaTime;
var sourceActive = source.gameObject.activeSelf;
var results = new System.Collections.Generic.List<object>();
var ground = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube);
ground.name = "Codex Temporary Verification Ground";
ground.transform.position = new UnityEngine.Vector3(0,99.5f,0);
ground.transform.localScale = new UnityEngine.Vector3(4000,1,4000);
UnityEngine.GameObject clone = null;
UnityEngine.GameObject target = null;
try
{
    source.gameObject.SetActive(false);
    UnityEngine.Physics.simulationMode = UnityEngine.SimulationMode.Script;
    foreach (var scenario in new[] {
        new {name="straight_150",goal=new UnityEngine.Vector3(0,100,1500),speed=150f,acc=50f,dt=0.02f,move=false},
        new {name="turn_150",goal=new UnityEngine.Vector3(15,100,60),speed=150f,acc=50f,dt=0.02f,move=false},
        new {name="behind",goal=new UnityEngine.Vector3(-5,100,-20),speed=150f,acc=50f,dt=0.02f,move=false},
        new {name="move_at_speed",goal=new UnityEngine.Vector3(0,100,1500),speed=150f,acc=50f,dt=0.02f,move=true},
        new {name="low_speed",goal=new UnityEngine.Vector3(4,100,20),speed=20f,acc=5f,dt=0.02f,move=false},
        new {name="turn_100hz",goal=new UnityEngine.Vector3(15,100,60),speed=150f,acc=50f,dt=0.01f,move=false},
        new {name="turn_25hz",goal=new UnityEngine.Vector3(15,100,60),speed=150f,acc=50f,dt=0.04f,move=false},
        new {name="scaled_3x",goal=new UnityEngine.Vector3(15,100,60),speed=150f,acc=50f,dt=0.02f,move=false},
        new {name="zero_speed",goal=new UnityEngine.Vector3(0,100,60),speed=0f,acc=50f,dt=0.02f,move=false},
        new {name="remove_target",goal=new UnityEngine.Vector3(0,100,60),speed=150f,acc=50f,dt=0.02f,move=false},
        new {name="restart_target",goal=new UnityEngine.Vector3(0,100,20),speed=150f,acc=50f,dt=0.02f,move=false},
        new {name="spline_smoke",goal=new UnityEngine.Vector3(0,100,60),speed=20f,acc=5f,dt=0.02f,move=false},
        new {name="spline_end",goal=new UnityEngine.Vector3(0,100,60),speed=30f,acc=50f,dt=0.02f,move=false}
    })
    {
        UnityEngine.Time.fixedDeltaTime=scenario.dt;
        clone=UnityEngine.Object.Instantiate(source.gameObject,new UnityEngine.Vector3(0,100.1f,0),UnityEngine.Quaternion.identity);
        clone.name="Codex Verification Car";
        if(scenario.name=="scaled_3x") clone.transform.localScale*=3f;
        target=new UnityEngine.GameObject("Codex Verification Target");
        target.transform.position=scenario.goal;
        var car=clone.GetComponent<GodOfSpeed.Racing.PhysicsSplineCarDriver>();
        type.GetField("targetObject",flags).SetValue(car,target);
        type.GetField("spline",flags).SetValue(car,null);
        type.GetField("speedKmh",flags).SetValue(car,scenario.speed);
        type.GetField("acceleration",flags).SetValue(car,scenario.acc);
        if(scenario.name=="spline_smoke" || scenario.name=="spline_end")
        {
            target.transform.position=new UnityEngine.Vector3(0,100,0);
            var path=target.AddComponent<UnityEngine.Splines.SplineContainer>();
            path.Spline=new UnityEngine.Splines.Spline();
            path.Spline.Add(new UnityEngine.Splines.BezierKnot(new Unity.Mathematics.float3(0,0,0)));
            path.Spline.Add(new UnityEngine.Splines.BezierKnot(new Unity.Mathematics.float3(0,0,100)));
            type.GetField("targetObject",flags).SetValue(car,null);
            type.GetField("spline",flags).SetValue(car,path);
        }
        clone.SetActive(true);
        var rb=clone.GetComponent<UnityEngine.Rigidbody>();
        rb.interpolation=UnityEngine.RigidbodyInterpolation.None;
        rb.linearVelocity=UnityEngine.Vector3.zero;
        rb.angularVelocity=UnityEngine.Vector3.zero;
        var wheels=clone.GetComponentsInChildren<UnityEngine.WheelCollider>();
        var tick=type.GetMethod("FixedUpdate",flags);
        var getOrigin=type.GetMethod("GetSteeringOrigin",flags);
        UnityEngine.Physics.SyncTransforms();
        for(int i=0;i<(int)(2/scenario.dt);i++) { tick.Invoke(car,null); UnityEngine.Physics.Simulate(scenario.dt); }
        type.GetField("isRunning",flags).SetValue(car,true);
        float maxSpeed=0,maxSideSpeed=0,maxYaw=0,minDistance=10000,endDistance=10000,maxSteeringDemand=0;
        int sliding=0,samples=0;
        bool finite=true,moved=false;
        float duration=scenario.name=="spline_smoke" ? 10f : scenario.name=="straight_150" || scenario.move ? 110f : 50f;
        for(int i=0;i<(int)(duration/scenario.dt);i++)
        {
            if(scenario.name=="remove_target" && i*scenario.dt>=3f) type.GetField("targetObject",flags).SetValue(car,null);
            if(scenario.name=="restart_target" && !moved && i*scenario.dt>=15f) { target.transform.position=new UnityEngine.Vector3(5,100,40); moved=true; }
            if(scenario.move && !moved && (rb.linearVelocity.magnitude>40f || i*scenario.dt>=40f)) { target.transform.position=rb.position+new UnityEngine.Vector3(60,0,60); moved=true; minDistance=10000; }
            tick.Invoke(car,null);
            float wheelbase=(float)type.GetField("wheelbase",flags).GetValue(car);
            var front=(UnityEngine.WheelCollider)type.GetField("frontLeftWheel",flags).GetValue(car);
            float planarSpeedSquared=UnityEngine.Vector3.ProjectOnPlane(rb.linearVelocity,UnityEngine.Vector3.up).sqrMagnitude;
            maxSteeringDemand=UnityEngine.Mathf.Max(maxSteeringDemand,planarSpeedSquared*UnityEngine.Mathf.Abs(UnityEngine.Mathf.Tan(front.steerAngle*UnityEngine.Mathf.Deg2Rad))/wheelbase);
            UnityEngine.Physics.Simulate(scenario.dt);
            var velocity=rb.linearVelocity;
            finite &= !float.IsNaN(velocity.sqrMagnitude) && !float.IsInfinity(velocity.sqrMagnitude);
            maxSpeed=UnityEngine.Mathf.Max(maxSpeed,velocity.magnitude*3.6f);
            maxSideSpeed=UnityEngine.Mathf.Max(maxSideSpeed,UnityEngine.Mathf.Abs(UnityEngine.Vector3.Dot(velocity,clone.transform.right)));
            maxYaw=UnityEngine.Mathf.Max(maxYaw,UnityEngine.Mathf.Abs(rb.angularVelocity.y)*UnityEngine.Mathf.Rad2Deg);
            endDistance=UnityEngine.Vector3.ProjectOnPlane(target.transform.position-(UnityEngine.Vector3)getOrigin.Invoke(car,null),UnityEngine.Vector3.up).magnitude;
            minDistance=UnityEngine.Mathf.Min(minDistance,endDistance);
            foreach(var wheel in wheels) if(wheel.GetGroundHit(out UnityEngine.WheelHit hit)) { samples++; if(UnityEngine.Mathf.Abs(hit.forwardSlip)>wheel.forwardFriction.extremumSlip) sliding++; }
        }
        float endSpeed=rb.linearVelocity.magnitude;
        if(scenario.name=="spline_end") endDistance=UnityEngine.Vector3.ProjectOnPlane(new UnityEngine.Vector3(0,100,100)-(UnityEngine.Vector3)getOrigin.Invoke(car,null),UnityEngine.Vector3.up).magnitude;
        bool arrivalExpected=scenario.name!="zero_speed" && scenario.name!="remove_target" && scenario.name!="spline_smoke";
        bool stoppedOrSpline=scenario.name=="spline_smoke" ? rb.position.z>1f && endSpeed>0.1f : endSpeed<0.15f;
        results.Add(new{scenario.name,pass=finite && (!arrivalExpected || endDistance<1f) && stoppedOrSpline && maxSideSpeed<2f && maxSteeringDemand<4.1f && (scenario.name!="straight_150" || maxSpeed>145f) && (scenario.name!="zero_speed" || maxSpeed<0.1f),maxSpeed,maxSideSpeed,maxYaw,minDistance,endDistance,endSpeed,maxSteeringDemand,sliding,samples});
        UnityEngine.Object.DestroyImmediate(clone); clone=null;
        UnityEngine.Object.DestroyImmediate(target); target=null;
    }
}
finally
{
    if(clone!=null) UnityEngine.Object.DestroyImmediate(clone);
    if(target!=null) UnityEngine.Object.DestroyImmediate(target);
    UnityEngine.Object.DestroyImmediate(ground);
    source.gameObject.SetActive(sourceActive);
    UnityEngine.Physics.simulationMode=oldMode;
    UnityEngine.Time.fixedDeltaTime=oldDelta;
}
var json=Newtonsoft.Json.JsonConvert.SerializeObject(results,Newtonsoft.Json.Formatting.Indented);
System.IO.File.WriteAllText("C:/GodOfSpeed/docs/TargetCarVerification.json",json);
return results;
