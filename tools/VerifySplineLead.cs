if(!UnityEditor.EditorApplication.isPlaying) throw new System.InvalidOperationException("Play Mode required.");
var source=UnityEngine.Object.FindFirstObjectByType<GodOfSpeed.Racing.PhysicsSplineCarDriver>();
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
var type=source.GetType();
var spline=(UnityEngine.Splines.SplineContainer)type.GetField("spline",flags).GetValue(source);
if(spline==null) throw new System.InvalidOperationException("Assign the scene spline before running this test.");
var oldMode=UnityEngine.Physics.simulationMode;
var active=source.gameObject.activeSelf;
UnityEngine.GameObject clone=null;
var rows=new System.Collections.Generic.List<object>();
try
{
    source.gameObject.SetActive(false);
    UnityEngine.Physics.simulationMode=UnityEngine.SimulationMode.Script;
    foreach(float limit in new[]{30f,150f})
    {
        clone=UnityEngine.Object.Instantiate(source.gameObject,source.transform.position,source.transform.rotation);
        clone.name="Codex Spline Verification Car";
        var car=clone.GetComponent<GodOfSpeed.Racing.PhysicsSplineCarDriver>();
        type.GetField("targetObject",flags).SetValue(car,null);
        type.GetField("speedKmh",flags).SetValue(car,limit);
        clone.SetActive(true);
        var rb=clone.GetComponent<UnityEngine.Rigidbody>();
        rb.interpolation=UnityEngine.RigidbodyInterpolation.None;
        rb.linearVelocity=UnityEngine.Vector3.zero;
        rb.angularVelocity=UnityEngine.Vector3.zero;
        var tick=type.GetMethod("FixedUpdate",flags);
        var getOrigin=type.GetMethod("GetSteeringOrigin",flags);
        var nearest=type.GetMethod("FindNearestSplinePosition",flags);
        float dt=UnityEngine.Time.fixedDeltaTime;
        for(int i=0;i<(int)(2f/dt);i++){tick.Invoke(car,null);UnityEngine.Physics.Simulate(dt);}
        type.GetField("isRunning",flags).SetValue(car,true);
        float maxSpeed=0,maxCrossTrack=0,maxLeadExcess=0,travelled=0;
        int laps=0;
        float lastProgress=(float)nearest.Invoke(car,null);
        var lastPosition=rb.position;
        for(int i=0;i<(int)(60f/dt);i++)
        {
            tick.Invoke(car,null);
            var origin=(UnityEngine.Vector3)getOrigin.Invoke(car,null);
            float t=(float)nearest.Invoke(car,null);
            var raw=spline.EvaluatePosition(t);
            var closest=new UnityEngine.Vector3(raw.x,raw.y,raw.z);
            float cross=UnityEngine.Vector3.Distance(origin,closest);
            var point=(UnityEngine.Vector3)type.GetField("targetPoint",flags).GetValue(car);
            float wb=(float)type.GetField("wheelbase",flags).GetValue(car);
            float lead=UnityEngine.Mathf.Max(wb*2f,UnityEngine.Vector3.ProjectOnPlane(rb.linearVelocity,UnityEngine.Vector3.up).magnitude*0.8f);
            maxLeadExcess=UnityEngine.Mathf.Max(maxLeadExcess,UnityEngine.Vector3.Distance(origin,point)-cross-lead);
            maxCrossTrack=UnityEngine.Mathf.Max(maxCrossTrack,cross);
            if(lastProgress>0.8f && t<0.2f) laps++;
            lastProgress=t;
            UnityEngine.Physics.Simulate(dt);
            maxSpeed=UnityEngine.Mathf.Max(maxSpeed,rb.linearVelocity.magnitude*3.6f);
            travelled+=UnityEngine.Vector3.Distance(lastPosition,rb.position);
            lastPosition=rb.position;
        }
        rb.linearVelocity=UnityEngine.Vector3.zero;
        rb.angularVelocity=UnityEngine.Vector3.zero;
        rb.constraints=UnityEngine.RigidbodyConstraints.FreezeAll;
        tick.Invoke(car,null);
        var frozenPoint=(UnityEngine.Vector3)type.GetField("targetPoint",flags).GetValue(car);
        for(int i=0;i<(int)(3f/dt);i++){tick.Invoke(car,null);UnityEngine.Physics.Simulate(dt);}
        float frozenDrift=UnityEngine.Vector3.Distance(frozenPoint,(UnityEngine.Vector3)type.GetField("targetPoint",flags).GetValue(car));
        rows.Add(new{limit,pass=maxLeadExcess<0.1f && frozenDrift<0.05f && travelled>100f && maxCrossTrack<1.5f && laps>=2,maxSpeed,maxCrossTrack,maxLeadExcess,travelled,laps,frozenDrift});
        UnityEngine.Object.DestroyImmediate(clone);clone=null;
    }
}
finally {if(clone!=null)UnityEngine.Object.DestroyImmediate(clone);source.gameObject.SetActive(active);UnityEngine.Physics.simulationMode=oldMode;}
System.IO.File.WriteAllText("C:/GodOfSpeed/docs/SplineLeadVerification.json",Newtonsoft.Json.JsonConvert.SerializeObject(rows,Newtonsoft.Json.Formatting.Indented));
return rows;
