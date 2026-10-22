if (!UnityEngine.Application.isPlaying) throw new System.Exception("Requires Play Mode");
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
var car = UnityEngine.Object.FindFirstObjectByType<GodOfSpeed.Racing.PhysicsSplineCarDriver>();
var hud = UnityEngine.Object.FindFirstObjectByType<GodOfSpeed.Racing.CarSpeedDisplay>();
var label = (TMPro.TMP_Text)hud.GetType().GetField("speedText", flags).GetValue(hud);
var main = car.GetType().GetField("mainCar", flags);
var timer = hud.GetType().GetField("nextSearchTime", flags);
var update = hud.GetType().GetMethod("Update", flags);
var body = car.GetComponent<UnityEngine.Rigidbody>();
var velocity = body.linearVelocity;
bool originalMain = car.IsMainCar;
bool paused = UnityEditor.EditorApplication.isPaused;
UnityEngine.GameObject clone = null;
var results = new System.Collections.Generic.List<string>();
System.Action<string, string, bool> check = (name, expected, visible) => {
    timer.SetValue(hud, 0f); update.Invoke(hud, null);
    if (label.enabled != visible || (visible && label.text != expected))
        throw new System.Exception(name + ": " + label.text + ", visible=" + label.enabled);
    results.Add(name + ": PASS");
};
try {
    UnityEditor.EditorApplication.isPaused = true;
    main.SetValue(car, true);
    body.linearVelocity = new UnityEngine.Vector3(10f, 0f, 0f);
    check("Actual speed 10 m/s", "36 км/ч", true);
    clone = UnityEngine.Object.Instantiate(car.gameObject, car.transform.position + UnityEngine.Vector3.up * 100f, car.transform.rotation);
    var other = clone.GetComponent<GodOfSpeed.Racing.PhysicsSplineCarDriver>();
    main.SetValue(other, false);
    clone.GetComponent<UnityEngine.Rigidbody>().linearVelocity = new UnityEngine.Vector3(20f, 0f, 0f);
    check("Ignore unmarked faster car", "36 км/ч", true);
    main.SetValue(car, false); main.SetValue(other, true);
    check("Switch Main Car", "72 км/ч", true);
    main.SetValue(other, false);
    check("No Main Car hides label", "", false);
    main.SetValue(other, true); clone.SetActive(false);
    check("Inactive Main Car hides label", "", false);
    main.SetValue(car, true); body.linearVelocity = UnityEngine.Vector3.zero;
    check("Stopped car", "0 км/ч", true);
    return string.Join("\n", results);
} finally {
    if (clone != null) UnityEngine.Object.DestroyImmediate(clone);
    main.SetValue(car, originalMain); body.linearVelocity = velocity;
    timer.SetValue(hud, 0f); update.Invoke(hud, null);
    UnityEditor.EditorApplication.isPaused = paused;
}
