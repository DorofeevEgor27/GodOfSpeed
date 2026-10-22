if (!UnityEditor.EditorApplication.isPlaying) throw new System.InvalidOperationException("Play Mode required.");
var source = System.Array.Find(UnityEngine.Object.FindObjectsByType<GodOfSpeed.Racing.PhysicsSplineCarDriver>(UnityEngine.FindObjectsSortMode.None), d => d.IsMainCar);
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
var type = source.GetType();
var oldMode = UnityEngine.Physics.simulationMode;
var oldDelta = UnityEngine.Time.fixedDeltaTime;
var rows = new System.Collections.Generic.List<object>();
bool active = source.gameObject.activeSelf;
UnityEngine.GameObject pathObject = null, clone = null;
try
{
    source.gameObject.SetActive(false);
    UnityEngine.Physics.simulationMode = UnityEngine.SimulationMode.Script;
    UnityEngine.Time.fixedDeltaTime = 0.02f;
    pathObject = new UnityEngine.GameObject("Temporary straight regression path");
    pathObject.transform.position = source.transform.position;
    pathObject.transform.rotation = source.transform.rotation;
    var container = pathObject.AddComponent<UnityEngine.Splines.SplineContainer>();
    var line = new UnityEngine.Splines.Spline();
    line.Add(new UnityEngine.Splines.BezierKnot(new Unity.Mathematics.float3(0f, 0f, -10f)));
    line.Add(new UnityEngine.Splines.BezierKnot(new Unity.Mathematics.float3(0f, 0f, 250f)));
    container.Spline = line;
    foreach (int level in new int[] { 0, 100, 1000 })
    {
        clone = UnityEngine.Object.Instantiate(source.gameObject, source.transform.position, source.transform.rotation);
        var driver = clone.GetComponent<GodOfSpeed.Racing.PhysicsSplineCarDriver>();
        var settings = clone.GetComponent<GodOfSpeed.Racing.CarCharacteristics>();
        settings.SetLevels(100, 100, System.Math.Min(level, 100), 100);
        settings.SetCardBonuses(0f, 0f, System.Math.Max(0, level - 100), 0f);
        var serialized = new UnityEditor.SerializedObject(settings);
        serialized.FindProperty("infiniteFuel").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        type.GetField("spline", flags).SetValue(driver, container);
        clone.SetActive(true);
        var body = clone.GetComponent<UnityEngine.Rigidbody>();
        body.interpolation = UnityEngine.RigidbodyInterpolation.None;
        body.linearVelocity = UnityEngine.Vector3.zero;
        body.angularVelocity = UnityEngine.Vector3.zero;
        UnityEngine.Physics.SyncTransforms();
        var step = type.GetMethod("FixedUpdate", flags);
        for (int i = 0; i < 100; i++) { step.Invoke(driver, null); UnityEngine.Physics.Simulate(0.02f); }
        type.GetField("isRunning", flags).SetValue(driver, true);
        float sum = 0f;
        int count = 0;
        var start = body.position;
        for (int i = 0; i < 1000; i++)
        {
            step.Invoke(driver, null);
            UnityEngine.Physics.Simulate(0.02f);
            if (i == 500) start = body.position;
            if (i > 500) { sum += driver.CurrentSpeedUnitsPerMinute; count++; }
        }
        float measured = sum / count;
        float displacementSpeed = UnityEngine.Vector3.ProjectOnPlane(body.position - start, UnityEngine.Vector3.up).magnitude / (499f * 0.02f) * 60f;
        float target = settings.SpeedUnitsPerMinute;
        rows.Add(new { level, target, measured, displacementSpeed, angle = driver.CurrentCornerAngle,
            pass = UnityEngine.Mathf.Abs(measured - target) < 1f && UnityEngine.Mathf.Abs(displacementSpeed - target) < 1f && driver.CurrentCornerAngle == 0f });
        UnityEngine.Object.DestroyImmediate(clone);
        clone = null;
    }
}
finally
{
    if (clone != null) UnityEngine.Object.DestroyImmediate(clone);
    if (pathObject != null) UnityEngine.Object.DestroyImmediate(pathObject);
    source.gameObject.SetActive(active);
    UnityEngine.Physics.simulationMode = oldMode;
    UnityEngine.Time.fixedDeltaTime = oldDelta;
}
string json = Newtonsoft.Json.JsonConvert.SerializeObject(rows, Newtonsoft.Json.Formatting.Indented);
System.IO.File.WriteAllText("C:/GodOfSpeed/artifacts/CorneringFormula/Straight.json", json);
return json;
