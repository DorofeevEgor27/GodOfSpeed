var config = UnityEditor.AssetDatabase.LoadAssetAtPath<GodOfSpeed.Racing.GlobalGameConfig>("Assets/Settings/GlobalGameConfig.asset");
var checks = new System.Collections.Generic.List<object>();
int failures = 0, count = 0;
System.Action<string, bool> check = (name, pass) => { count++; if (!pass) { failures++; checks.Add(new { name, pass }); } };
System.Func<float, float, bool> near = (a, b) => UnityEngine.Mathf.Abs(a - b) < 0.00001f;
check("180/0", near(config.GetCornerSpeedMultiplier(180f, 0), 0.1f));
check("180/100", near(config.GetCornerSpeedMultiplier(180f, 100), 0.6f));
check("180/200", near(config.GetCornerSpeedMultiplier(180f, 200), 0.725f));
check("60/100 full", near(config.GetCornerSpeedMultiplier(60f, 100), 1f));
check("30/0 minimal loss", near(config.GetCornerSpeedMultiplier(30f, 0), 0.975f));
check("card level beyond 100", GodOfSpeed.Racing.GlobalGameConfig.GetEffectiveCorneringLevel(100, 100f) == 200);
check("zero level retained", GodOfSpeed.Racing.GlobalGameConfig.GetEffectiveCorneringLevel(0, 100f) == 0);
check("permanent level capped", GodOfSpeed.Racing.GlobalGameConfig.GetEffectiveCorneringLevel(200, 0f) == 100);
for (int level = 0; level <= 1000; level++)
{
    float previous = 1f;
    for (int angle = 0; angle <= 200; angle++)
    {
        float value = config.GetCornerSpeedMultiplier(angle, level);
        check("finite bounded", float.IsFinite(value) && value >= 0f && value <= 1f);
        check("angle monotonic", value <= previous + 0.000001f);
        check("level monotonic", value + 0.000001f >= config.GetCornerSpeedMultiplier(angle, level - 1));
        if (angle > 180) check("angle saturates", near(value, config.GetCornerSpeedMultiplier(180, level)));
        previous = value;
    }
    check("straight unchanged", near(config.GetCornerSpeedMultiplier(0f, level), 1f));
    check("hairpin never faster than 85%", config.GetCornerSpeedMultiplier(180f, level) <= 0.850001f);
}
var distances = new float[] { 0f, 1f, 2f, 3f, 4f, 5f };
System.Func<float, UnityEngine.Vector3> heading = a => UnityEngine.Quaternion.AngleAxis(a, UnityEngine.Vector3.up) * UnityEngine.Vector3.forward;
var profile = new GodOfSpeed.Racing.SplineCornerSpeedProfile(distances,
    new UnityEngine.Vector3[] { heading(0), heading(60), heading(120), heading(200), heading(200), heading(140) }, false);
check("whole bend not sample angle", profile.Corners.Count == 2 && near(profile.Corners[0].AngleDegrees, 180f));
check("opposite bend separate", profile.Corners.Count == 2 && UnityEngine.Mathf.Abs(profile.Corners[1].AngleDegrees - 60f) < 0.001f);
var rows = new System.Collections.Generic.List<object>();
foreach (int angle in new int[] { 0, 30, 40, 60, 90, 120, 150, 180, 220 })
    rows.Add(new { angle, level0 = config.GetCornerSpeedMultiplier(angle, 0), level50 = config.GetCornerSpeedMultiplier(angle, 50), level100 = config.GetCornerSpeedMultiplier(angle, 100), level200 = config.GetCornerSpeedMultiplier(angle, 200), limit = config.GetCornerSpeedMultiplier(angle, int.MaxValue) });
var result = new { count, failures, pass = failures == 0, checks, rows };
string json = Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented);
System.IO.File.WriteAllText("C:/GodOfSpeed/artifacts/CorneringFormula/Formula.json", json);
return json;
