using System;
using UnityEngine;

namespace GodOfSpeed.Racing
{
    [CreateAssetMenu(fileName = "GlobalGameConfig", menuName = "SpeedGod/Общий конфиг")]
    public sealed class GlobalGameConfig : ScriptableObject
    {
        public const int MinimumLevel = 1;
        public const int MinimumSpeedLevel = 0;
        public const int MaximumLevel = 100;

        [SerializeField, InspectorName("Скорость (юниты/мин; на спидометре км/ч)")]
        private SpeedProgression speed = new SpeedProgression();

        [SerializeField, InspectorName("Ускорение (м/с²)")]
        private LevelProgression acceleration = new LevelProgression(1f, 0.25f);

        [SerializeField, InspectorName("Поворот (боковое ускорение, м/с²)")]
        [Tooltip("Более высокое значение позволяет быстрее проходить изгибы. Скорость зависит от радиуса поворота.")]
        private LevelProgression cornering = new LevelProgression(1f, 0.1f);

        [SerializeField, InspectorName("Топливо (вместимость бака)")]
        private LevelProgression fuelCapacity = new LevelProgression(500f, 1f);

        [SerializeField, Range(0f, 1f), InspectorName("Доля скорости при пустом баке")]
        private float emptyFuelSpeedMultiplier = 0.2f;

        public float GetSpeedUnitsPerMinute(int level) => speed.Evaluate(level);
        public float GetAcceleration(int level) => acceleration.Evaluate(level);
        public float GetCorneringAcceleration(int level) => cornering.Evaluate(level);
        public float GetFuelCapacity(int level) => fuelCapacity.Evaluate(level);
        public float EmptyFuelSpeedMultiplier => Mathf.Clamp01(emptyFuelSpeedMultiplier);

        public static int GetEffectiveLevel(int permanentLevel, float cardBonusPercent)
            => CalculateEffectiveLevel(permanentLevel, cardBonusPercent, MinimumLevel, MaximumLevel);

        public static int GetEffectiveSpeedLevel(int permanentLevel, float cardBonusPercent)
            => CalculateEffectiveLevel(permanentLevel, cardBonusPercent, MinimumSpeedLevel, int.MaxValue);

        private static int CalculateEffectiveLevel(int permanentLevel, float cardBonusPercent,
            int minimumLevel, int maximumEffectiveLevel)
        {
            permanentLevel = Mathf.Clamp(permanentLevel, minimumLevel, MaximumLevel);
            if (float.IsNaN(cardBonusPercent) || float.IsInfinity(cardBonusPercent))
                throw new ArgumentOutOfRangeException(nameof(cardBonusPercent));

            // Use double precision so an exact integer bonus does not gain an extra level.
            double level = permanentLevel * (100d + cardBonusPercent) / 100d;
            level = Math.Max(minimumLevel, Math.Min(maximumEffectiveLevel, level));
            return (int)Math.Ceiling(level);
        }

        [Serializable]
        private sealed class SpeedProgression
        {
            [SerializeField, Min(0f), InspectorName("Скорость на уровне 0")]
            private float levelZeroSpeed = 70f;

            [SerializeField, Min(0f), InspectorName("Скорость на уровне 100")]
            private float levelHundredSpeed = 200f;

            public float Evaluate(int effectiveLevel)
            {
                float minimum = Mathf.Max(0f, levelZeroSpeed);
                float maximum = Mathf.Max(minimum, levelHundredSpeed);
                // Extrapolate beyond 100 for card bonuses; only permanent upgrades are capped.
                return minimum + (maximum - minimum) * (Mathf.Max(0, effectiveLevel) / 100f);
            }
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
