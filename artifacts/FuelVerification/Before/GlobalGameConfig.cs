using System;
using UnityEngine;

namespace GodOfSpeed.Racing
{
    [CreateAssetMenu(fileName = "GlobalGameConfig", menuName = "SpeedGod/Общий конфиг")]
    public sealed class GlobalGameConfig : ScriptableObject
    {
        public const int MinimumLevel = 1;
        public const int MaximumLevel = 100;

        [SerializeField, InspectorName("Скорость (км/ч)")]
        private LevelProgression speed = new LevelProgression(5f, 0.5f);

        [SerializeField, InspectorName("Ускорение (м/с²)")]
        private LevelProgression acceleration = new LevelProgression(1f, 0.25f);

        [SerializeField, InspectorName("Поворот (боковое ускорение, м/с²)")]
        [Tooltip("Более высокое значение позволяет быстрее проходить изгибы. Скорость зависит от радиуса поворота.")]
        private LevelProgression cornering = new LevelProgression(1f, 0.1f);

        [SerializeField, InspectorName("Топливо (вместимость бака)")]
        private LevelProgression fuelCapacity = new LevelProgression(10f, 1f);

        public float GetSpeedKmh(int level) => speed.Evaluate(level);
        public float GetAcceleration(int level) => acceleration.Evaluate(level);
        public float GetCorneringAcceleration(int level) => cornering.Evaluate(level);
        public float GetFuelCapacity(int level) => fuelCapacity.Evaluate(level);

        public static int GetEffectiveLevel(int permanentLevel, float cardBonusPercent)
        {
            permanentLevel = Mathf.Clamp(permanentLevel, MinimumLevel, MaximumLevel);
            if (float.IsNaN(cardBonusPercent) || float.IsInfinity(cardBonusPercent))
                throw new ArgumentOutOfRangeException(nameof(cardBonusPercent));

            // Use double precision so an exact integer bonus does not gain an extra level.
            double level = permanentLevel * (1d + cardBonusPercent / 100d);
            level = Math.Max(MinimumLevel, Math.Min(MaximumLevel, level));
            return (int)Math.Ceiling(level);
        }

        [Serializable]
        private sealed class LevelProgression
        {
            [SerializeField, Min(0.001f), InspectorName("Минимум (уровень 1)")]
            private float minimum;

            [SerializeField, Min(0f), InspectorName("Прибавка за уровень")]
            private float increasePerLevel;

            public LevelProgression(float minimum, float increasePerLevel)
            {
                this.minimum = minimum;
                this.increasePerLevel = increasePerLevel;
            }

            public float Evaluate(int level)
            {
                level = Mathf.Clamp(level, MinimumLevel, MaximumLevel);
                return Mathf.Max(0.001f, minimum) + (level - MinimumLevel) * Mathf.Max(0f, increasePerLevel);
            }
        }
    }
}
