using TMPro;
using UnityEngine;

namespace GodOfSpeed.Racing
{
    [DisallowMultipleComponent]
    public sealed class CarSpeedDisplay : MonoBehaviour
    {
        [SerializeField] private TMP_Text speedText;

        private PhysicsSplineCarDriver selectedCar;
        private float nextSearchTime;
        private int lastDisplayedSpeed = -1;
        private bool warnedAboutMultipleCars;

        private void OnEnable()
        {
            nextSearchTime = 0f;
            lastDisplayedSpeed = -1;
        }

        private void Update()
        {
            if (speedText == null) return;

            if (Time.unscaledTime >= nextSearchTime)
            {
                FindMainCar();
                nextSearchTime = Time.unscaledTime + 0.2f;
            }

            bool hasCar = selectedCar != null && selectedCar.isActiveAndEnabled && selectedCar.IsMainCar;
            speedText.enabled = hasCar;
            if (!hasCar)
            {
                lastDisplayedSpeed = -1;
                return;
            }

            int speed = Mathf.Max(0, Mathf.RoundToInt(selectedCar.CurrentSpeedKmh));
            if (speed != lastDisplayedSpeed)
            {
                speedText.SetText("{0} км/ч", speed);
                lastDisplayedSpeed = speed;
            }
        }

        private void FindMainCar()
        {
            PhysicsSplineCarDriver firstMarkedCar = null;
            int markedCount = 0;
            foreach (PhysicsSplineCarDriver car in FindObjectsByType<PhysicsSplineCarDriver>(FindObjectsSortMode.InstanceID))
            {
                if (!car.isActiveAndEnabled || !car.IsMainCar) continue;
                if (firstMarkedCar == null) firstMarkedCar = car;
                markedCount++;
            }

            // Keep the current selection stable if a duplicate is accidentally marked.
            if (selectedCar == null || !selectedCar.isActiveAndEnabled || !selectedCar.IsMainCar)
                selectedCar = firstMarkedCar;

            if (markedCount > 1 && !warnedAboutMultipleCars)
                Debug.LogWarning("More than one active car is marked Main Car. The speed display keeps its current car. Mark only one car to select it unambiguously.", this);
            warnedAboutMultipleCars = markedCount > 1;
        }

        private void OnDisable()
        {
            if (speedText != null) speedText.enabled = false;
            selectedCar = null;
        }
    }
}
