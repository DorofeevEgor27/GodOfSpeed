// One isolated, fixed-step Play Mode run on the owner's SupeAngle geometry.
// Select a Turns level through SessionState: CornerWork.Level (default 1).
if (!UnityEditor.EditorApplication.isPlaying)
    throw new System.InvalidOperationException("Play Mode required.");
if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "SupeAngle")
    throw new System.InvalidOperationException("Open SupeAngle first.");
var drivers = UnityEngine.Object.FindObjectsByType<GodOfSpeed.Racing.PhysicsSplineCarDriver>(UnityEngine.FindObjectsSortMode.None);
var source = System.Array.Find(drivers, d => d.IsMainCar);
if (source == null) throw new System.InvalidOperationException("No active Main Car.");
int level = UnityEditor.SessionState.GetInt("CornerWork.Level", 1);
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
var type = source.GetType();
var spline = (UnityEngine.Splines.SplineContainer)type.GetField("spline", flags).GetValue(source);
if (spline == null || spline.Spline.Closed) throw new System.InvalidOperationException("An open spline is required.");
var oldMode = UnityEngine.Physics.simulationMode;
var oldDelta = UnityEngine.Time.fixedDeltaTime;
bool oldActive = source.gameObject.activeSelf;
UnityEngine.GameObject clone = null;
var samples = new System.Collections.Generic.List<object>();
var fuelChecks = new System.Collections.Generic.List<object>();
object result = null;
try
{
    source.gameObject.SetActive(false);
    UnityEngine.Physics.simulationMode = UnityEngine.SimulationMode.Script;
    UnityEngine.Time.fixedDeltaTime = 0.02f;
    clone = UnityEngine.Object.Instantiate(source.gameObject, source.transform.position, source.transform.rotation);
    clone.name = "Cornering Verification Car";
    var driver = clone.GetComponent<GodOfSpeed.Racing.PhysicsSplineCarDriver>();
    var characteristics = clone.GetComponent<GodOfSpeed.Racing.CarCharacteristics>();
    characteristics.SetLevels(100, 100, System.Math.Min(100, level), 100);
    characteristics.SetCardBonuses(0f, 0f, System.Math.Max(0, level - 100), 0f);
    var settings = new UnityEditor.SerializedObject(characteristics);
    settings.FindProperty("infiniteFuel").boolValue = true;
    settings.ApplyModifiedPropertiesWithoutUndo();
    clone.SetActive(true);
    var body = clone.GetComponent<UnityEngine.Rigidbody>();
    body.interpolation = UnityEngine.RigidbodyInterpolation.None;
    body.linearVelocity = UnityEngine.Vector3.zero;
    body.angularVelocity = UnityEngine.Vector3.zero;
    var tank = clone.GetComponent<GodOfSpeed.Racing.CarFuelTank>();
    tank.FillToCapacity();
    double capacity = characteristics.FuelCapacity;
    tank.Consume(10000d, 1000d);
    fuelChecks.Add(new { test = "infinite_no_consumption", pass = tank.CurrentFuel == capacity && !tank.IsEmpty, actual = tank.CurrentFuel });
    settings.Update(); settings.FindProperty("infiniteFuel").boolValue = false; settings.ApplyModifiedPropertiesWithoutUndo();
    tank.FillToCapacity(); tank.Consume(20d, 1d);
    fuelChecks.Add(new { test = "disabled_restores_consumption", pass = tank.CurrentFuel == capacity - 20d, actual = tank.CurrentFuel });
    tank.Consume(10000d, 1000d);
    fuelChecks.Add(new { test = "disabled_can_empty", pass = tank.CurrentFuel == 0d && tank.IsEmpty, actual = tank.CurrentFuel });
    settings.Update(); settings.FindProperty("infiniteFuel").boolValue = true; settings.ApplyModifiedPropertiesWithoutUndo();
    fuelChecks.Add(new { test = "enable_on_empty_is_full", pass = tank.CurrentFuel == capacity && !tank.IsEmpty, actual = tank.CurrentFuel });
    tank.Consume(0d, 0d);

    var fixedUpdate = type.GetMethod("FixedUpdate", flags);
    var steeringOrigin = type.GetMethod("GetSteeringOrigin", flags);
    var nearest = type.GetMethod("FindNearestSplinePosition", flags);
    var distanceAt = type.GetMethod("GetDistanceAtProgress", flags);
    var allowed = type.GetField("currentAllowedSpeedMps", flags);
    UnityEngine.Physics.SyncTransforms();
    for (int i = 0; i < 100; i++) { fixedUpdate.Invoke(driver, null); UnityEngine.Physics.Simulate(0.02f); }
    float length = (float)type.GetField("splineLength", flags).GetValue(driver);
    var road = UnityEngine.Object.FindFirstObjectByType<GodOfSpeed.Racing.SplineRoad>();
    float width = road != null ? new UnityEditor.SerializedObject(road).FindProperty("width").floatValue : 3f;
    type.GetField("isRunning", flags).SetValue(driver, true);
    float startArc = (float)distanceAt.Invoke(driver, new object[] { (float)nearest.Invoke(driver, null) });
    float maxCross = 0f, maxSpeed = 0f, travelled = 0f, maxArc = startArc, lastArc = startArc;
    float timeTo95 = -1f, finishTime = -1f, timeSinceAdvance = 0f, seconds = 0f;
    int jumps = 0, offRoadSamples = 0;
    bool finite = true;
    var lastPosition = body.position;
    var elapsed = System.Diagnostics.Stopwatch.StartNew();
    for (int step = 0; step < 30000; step++)
    {
        fixedUpdate.Invoke(driver, null);
        UnityEngine.Physics.Simulate(0.02f);
        seconds = (step + 1) * 0.02f;
        travelled += UnityEngine.Vector3.Distance(lastPosition, body.position);
        lastPosition = body.position;
        float speed = driver.CurrentSpeedUnitsPerMinute;
        finite &= float.IsFinite(speed) && float.IsFinite(body.position.sqrMagnitude);
        maxSpeed = UnityEngine.Mathf.Max(maxSpeed, speed);
        if (step % 10 != 0) continue;
        float t = (float)nearest.Invoke(driver, null);
        float arc = (float)distanceAt.Invoke(driver, new object[] { t });
        var origin = (UnityEngine.Vector3)steeringOrigin.Invoke(driver, null);
        var p = spline.EvaluatePosition(t);
        float cross = UnityEngine.Vector3.ProjectOnPlane(origin - new UnityEngine.Vector3(p.x, p.y, p.z), UnityEngine.Vector3.up).magnitude;
        maxCross = UnityEngine.Mathf.Max(maxCross, cross);
        if (cross > width * 0.5f) offRoadSamples++;
        if (System.Math.Abs(arc - lastArc) > speed / 60f * 0.2f * 4f + 3f) jumps++;
        lastArc = arc;
        if (arc > maxArc + 0.05f) { maxArc = arc; timeSinceAdvance = 0f; } else timeSinceAdvance += 0.2f;
        samples.Add(new { seconds, arc, t, speed, allowed = (float)allowed.GetValue(driver) * 60f, angle = driver.CurrentCornerAngle, multiplier = driver.CurrentCornerSpeedMultiplier, cross, x = body.position.x, y = body.position.y, z = body.position.z });
        if (timeTo95 < 0f && arc >= length * 0.95f) timeTo95 = seconds;
        if (arc >= length - 1.5f && speed < 9f) { finishTime = seconds; break; }
        if (!finite || timeSinceAdvance > 30f || cross > 15f || elapsed.Elapsed.TotalSeconds > 40d) break;
    }
    bool fullFuel = tank.CurrentFuel == capacity && !tank.IsEmpty;
    result = new {
        level, effectiveLevel = characteristics.EffectiveCorneringLevel, speedLevel = 100, accelerationLevel = 100, fuelLevel = 100,
        maximumSpeed = characteristics.SpeedUnitsPerMinute, corneringAcceleration = characteristics.CorneringAcceleration,
        splineLength = length, startArc, width, seconds, timeTo95, finishTime, maxArc, maxCross, maxSpeed, travelled,
        jumps, offRoadSamples, finite, fullFuel, fuel = tank.CurrentFuel,
        pass = finite && fullFuel && finishTime >= 0f && maxCross <= width * 0.5f && jumps == 0,
        wallSeconds = elapsed.Elapsed.TotalSeconds, fuelChecks, samples
    };
}
finally
{
    if (clone != null) UnityEngine.Object.DestroyImmediate(clone);
    source.gameObject.SetActive(oldActive);
    UnityEngine.Physics.simulationMode = oldMode;
    UnityEngine.Time.fixedDeltaTime = oldDelta;
}
string json = Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented);
System.IO.File.WriteAllText("C:/GodOfSpeed/artifacts/CorneringFormula/Turns-" + level + ".json", json);
return Newtonsoft.Json.Linq.JObject.Parse(json).ToString(Newtonsoft.Json.Formatting.None).Length > 0
    ? "Saved Turns-" + level + ".json; inspect summary and samples." : "No result";
