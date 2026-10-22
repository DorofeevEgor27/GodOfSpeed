if (!UnityEditor.EditorApplication.isPlaying) throw new System.Exception("Enter Play Mode first.");
if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "SuperSpeed")
    throw new System.Exception("Open the owner's SuperSpeed scene first.");
var drivers = UnityEngine.Object.FindObjectsByType<GodOfSpeed.Racing.PhysicsSplineCarDriver>(UnityEngine.FindObjectsSortMode.None);
var driver = System.Array.Find(drivers, d => d.IsMainCar);
if (driver == null) throw new System.Exception("No active Main Car in SuperSpeed.");
var car = driver.GetComponent<GodOfSpeed.Racing.CarCharacteristics>();
var body = driver.GetComponent<UnityEngine.Rigidbody>();
var tank = driver.GetComponent<GodOfSpeed.Racing.CarFuelTank>();
var serialized = new UnityEditor.SerializedObject(car);
int acceleration = serialized.FindProperty("accelerationLevel").intValue;
int turns = serialized.FindProperty("corneringLevel").intValue;
int fuel = serialized.FindProperty("fuelLevel").intValue;
var origin = body.position;
var rotation = body.rotation;
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
var running = typeof(GodOfSpeed.Racing.PhysicsSplineCarDriver).GetField("isRunning", flags);
var driveAcceleration = typeof(GodOfSpeed.Racing.PhysicsSplineCarDriver).GetField("driveAcceleration", flags);
var cruiseCorrection = typeof(GodOfSpeed.Racing.PhysicsSplineCarDriver).GetField("cruiseCorrectionAcceleration", flags);
var allowed = typeof(GodOfSpeed.Racing.PhysicsSplineCarDriver).GetField("currentAllowedSpeedMps", flags);
var display = UnityEngine.Object.FindFirstObjectByType<GodOfSpeed.Racing.CarSpeedDisplay>();
var hud = new UnityEditor.SerializedObject(display).FindProperty("speedText").objectReferenceValue as TMPro.TMP_Text;
var levels = new int[] { 0, 1, 50, 100 };
int index = 0;
var rows = new System.Collections.Generic.List<object>();
double started = 0d, measuredFrom = -1d;
int measuredFrame = 0;
var measurePosition = UnityEngine.Vector3.zero;
float minimum = float.MaxValue, maximum = 0f;
System.Action begin = () => {
    body.position = origin;
    body.rotation = rotation;
    body.linearVelocity = UnityEngine.Vector3.zero;
    body.angularVelocity = UnityEngine.Vector3.zero;
    car.SetLevels(levels[index], acceleration, turns, fuel);
    car.SetCardBonuses(0f, 0f, 0f, 0f);
    tank.FillToCapacity();
    driveAcceleration.SetValue(driver, 0f);
    if (cruiseCorrection != null) cruiseCorrection.SetValue(driver, 0f);
    running.SetValue(driver, true);
    started = UnityEngine.Time.timeAsDouble;
    measuredFrom = -1d;
    minimum = float.MaxValue;
    maximum = 0f;
    UnityEditor.SessionState.SetString("SpeedWork.Progress", "Testing level " + levels[index]);
};
begin();
UnityEditor.SessionState.SetBool("SpeedWork.ProbeDone", false);
UnityEditor.EditorApplication.CallbackFunction tick = null;
tick = () => {
    if (!UnityEditor.EditorApplication.isPlaying || driver == null) {
        UnityEditor.EditorApplication.update -= tick;
        return;
    }
    double now = UnityEngine.Time.timeAsDouble;
    if (now - started < 10d) return;
    if (measuredFrom < 0d) {
        measuredFrom = now;
        measurePosition = body.position;
        measuredFrame = UnityEngine.Time.frameCount;
    }
    float actual = driver.CurrentSpeedUnitsPerMinute;
    minimum = UnityEngine.Mathf.Min(minimum, actual);
    maximum = UnityEngine.Mathf.Max(maximum, actual);
    if (now - measuredFrom < 10d) return;
    double seconds = now - measuredFrom;
    float distance = UnityEngine.Vector3.Distance(measurePosition, body.position);
    float target = car.SpeedUnitsPerMinute;
    double average = distance / seconds * 60d;
    rows.Add(new {
        level = levels[index], targetUnitsPerMinute = target,
        actualUnitsPerMinute = actual, averageFromDisplacement = average,
        distanceUnits = distance, seconds, min = minimum, max = maximum,
        allowedUnitsPerMinute = (float)allowed.GetValue(driver) * 60f,
        hud = hud.text, frames = UnityEngine.Time.frameCount - measuredFrame,
        fuel = tank.CurrentFuel,
        pass = System.Math.Abs(average - target) <= 0.5d && UnityEngine.Mathf.RoundToInt(actual) == UnityEngine.Mathf.RoundToInt(target)
    });
    UnityEditor.SessionState.SetString("SpeedWork.ProbeReport", Newtonsoft.Json.JsonConvert.SerializeObject(rows));
    index++;
    if (index < levels.Length) {
        begin();
        return;
    }
    UnityEditor.EditorApplication.update -= tick;
    UnityEditor.SessionState.SetString("SpeedWork.Progress", "Completed");
    UnityEditor.SessionState.SetBool("SpeedWork.ProbeDone", true);
};
UnityEditor.EditorApplication.update += tick;
return "Started SuperSpeed probe: 10 seconds to settle + 10 seconds measured for levels 0, 1, 50, 100. Remaining characteristics unchanged.";
