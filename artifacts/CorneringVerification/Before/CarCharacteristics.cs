using UnityEngine;

namespace GodOfSpeed.Racing
{
    [DisallowMultipleComponent]
    [AddComponentMenu("SpeedGod/Характеристики машины")]
    public sealed class CarCharacteristics : MonoBehaviour
    {
        [SerializeField, InspectorName("Общий конфиг")]
        private GlobalGameConfig config;

        [SerializeField, Range(GlobalGameConfig.MinimumSpeedLevel, GlobalGameConfig.MaximumLevel), InspectorName("Скорость")]
        private int speedLevel = 1;

        [SerializeField, Range(GlobalGameConfig.MinimumLevel, GlobalGameConfig.MaximumLevel), InspectorName("Ускорение")]
        private int accelerationLevel = 1;

        [SerializeField, Range(GlobalGameConfig.MinimumLevel, GlobalGameConfig.MaximumLevel), InspectorName("Поворот")]
        private int corneringLevel = 1;

        [SerializeField, Range(GlobalGameConfig.MinimumLevel, GlobalGameConfig.MaximumLevel), InspectorName("Топливо")]
        private int fuelLevel = 1;

        private float speedBonusPercent;
        private float accelerationBonusPercent;
        private float corneringBonusPercent;
        private float fuelBonusPercent;

        public bool HasConfig => config != null;
        public int EffectiveSpeedLevel => GlobalGameConfig.GetEffectiveSpeedLevel(speedLevel, speedBonusPercent);
        public int EffectiveAccelerationLevel => GlobalGameConfig.GetEffectiveLevel(accelerationLevel, accelerationBonusPercent);
        public int EffectiveCorneringLevel => GlobalGameConfig.GetEffectiveLevel(corneringLevel, corneringBonusPercent);
        public int EffectiveFuelLevel => GlobalGameConfig.GetEffectiveLevel(fuelLevel, fuelBonusPercent);

        public float SpeedUnitsPerMinute => config.GetSpeedUnitsPerMinute(EffectiveSpeedLevel);
        public float Acceleration => config.GetAcceleration(EffectiveAccelerationLevel);
        public float CorneringAcceleration => config.GetCorneringAcceleration(EffectiveCorneringLevel);
        public float FuelCapacity => config.GetFuelCapacity(EffectiveFuelLevel);
        public float EmptyFuelSpeedMultiplier => config.EmptyFuelSpeedMultiplier;

        public void SetLevels(int speed, int acceleration, int cornering, int fuel)
        {
            speedLevel = Mathf.Clamp(speed, GlobalGameConfig.MinimumSpeedLevel, GlobalGameConfig.MaximumLevel);
            accelerationLevel = Mathf.Clamp(acceleration, GlobalGameConfig.MinimumLevel, GlobalGameConfig.MaximumLevel);
            corneringLevel = Mathf.Clamp(cornering, GlobalGameConfig.MinimumLevel, GlobalGameConfig.MaximumLevel);
            fuelLevel = Mathf.Clamp(fuel, GlobalGameConfig.MinimumLevel, GlobalGameConfig.MaximumLevel);
        }

        // The future card system passes summed percentages without changing permanent levels.
        public void SetCardBonuses(float speed, float acceleration, float cornering, float fuel)
        {
            // Validate all inputs before applying any of them.
            GlobalGameConfig.GetEffectiveSpeedLevel(speedLevel, speed);
            GlobalGameConfig.GetEffectiveLevel(accelerationLevel, acceleration);
            GlobalGameConfig.GetEffectiveLevel(corneringLevel, cornering);
            GlobalGameConfig.GetEffectiveLevel(fuelLevel, fuel);
            speedBonusPercent = speed;
            accelerationBonusPercent = acceleration;
            corneringBonusPercent = cornering;
            fuelBonusPercent = fuel;
        }

        private void OnValidate()
        {
            SetLevels(speedLevel, accelerationLevel, corneringLevel, fuelLevel);
        }
    }
}
