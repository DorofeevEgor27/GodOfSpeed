using System;
using UnityEngine;

namespace GodOfSpeed.Racing
{
    [CreateAssetMenu(fileName = "GlobalGameConfig", menuName = "SpeedGod/Общий конфиг")]
    public sealed class GlobalGameConfig : ScriptableObject
    {
        public const int MinimumLevel = 1;
        public const int MinimumSpeedLevel = 0;
        public const int MinimumCorneringLevel = 0;
        public const int MaximumLevel = 100;

        [SerializeField, InspectorName("Скорость (юниты/мин; на спидометре км/ч)")]
        private SpeedProgression speed = new SpeedProgression();

        [SerializeField, InspectorName("Ускорение (м/с²)")]
        private LevelProgression acceleration = new LevelProgression(1f, 0.25f);

        // Retain the old serialized tuning for legacy object-target driving.
        [SerializeField, HideInInspector]
        private LevelProgression cornering = new LevelProgression(1f, 0.1f);

        [SerializeField, InspectorName("Поворот (угол и потеря скорости)")]
        private CorneringSpeedProgression corneringSpeed = new CorneringSpeedProgression();

        [SerializeField, InspectorName("Топливо (вместимость бака)")]
        private LevelProgression fuelCapacity = new LevelProgression(500f, 1f);

        [SerializeField, Range(0f, 1f), InspectorName("Доля скорости при пустом баке")]
        private float emptyFuelSpeedMultiplier = 0.2f;

        public float GetSpeedUnitsPerMinute(int level) => speed.Evaluate(level);
        public float GetAcceleration(int level) => acceleration.Evaluate(level);
        public float GetCorneringAcceleration(int level) => cornering.Evaluate(level);
        public float GetCornerSpeedMultiplier(float angleDegrees, int level) => corneringSpeed.Evaluate(angleDegrees, level);
        public float GetFuelCapacity(int level) => fuelCapacity.Evaluate(level);
        public float EmptyFuelSpeedMultiplier => Mathf.Clamp01(emptyFuelSpeedMultiplier);

        public static int GetEffectiveLevel(int permanentLevel, float cardBonusPercent)
            => CalculateEffectiveLevel(permanentLevel, cardBonusPercent, MinimumLevel, MaximumLevel);

        public static int GetEffectiveSpeedLevel(int permanentLevel, float cardBonusPercent)
            => CalculateEffectiveLevel(permanentLevel, cardBonusPercent, MinimumSpeedLevel, int.MaxValue);

        public static int GetEffectiveCorneringLevel(int permanentLevel, float cardBonusPercent)
            => CalculateEffectiveLevel(permanentLevel, cardBonusPercent, MinimumCorneringLevel, int.MaxValue);

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
        private sealed class CorneringSpeedProgression
        {
            [SerializeField, Range(0f, 1f), InspectorName("Потеря на 180°: уровень 0")]
            private float lossAtZero = 0.9f;
            [SerializeField, Range(0f, 1f), InspectorName("Потеря на 180°: уровень 100")]
            private float lossAtHundred = 0.4f;
            [SerializeField, Range(0f, 1f), InspectorName("Предел потери на 180°")]
            private float minimumLoss = 0.15f;
            [SerializeField, Min(1f), InspectorName("Уровней сверх 100 на половину остатка улучшения")]
            private float bonusHalfLife = 100f;
            [SerializeField, Min(1f), InspectorName("Степень влияния угла")]
            private float anglePower = 2f;
            [SerializeField, Range(0f, 1f), InspectorName("Компенсация плавных поворотов на уровне 100")]
            private float gentleTurnRelief = 0.05f;

            public float Evaluate(float angleDegrees, int effectiveLevel)
            {
                if (float.IsNaN(angleDegrees) || float.IsInfinity(angleDegrees))
                    throw new ArgumentOutOfRangeException(nameof(angleDegrees));
                float level = Mathf.Max(0, effectiveLevel);
                float floor = Mathf.Clamp01(minimumLoss);
                float hundred = Mathf.Clamp(lossAtHundred, floor, 1f);
                float zero = Mathf.Clamp(lossAtZero, hundred, 1f);
                float fullTurnLoss = level <= 100f
                    ? Mathf.Lerp(zero, hundred, level / 100f)
                    : floor + (hundred - floor) * Mathf.Pow(2f, -(level - 100f) / Mathf.Max(1f, bonusHalfLife));
                // Accumulated bends beyond a half-turn saturate, rather than wrapping.
                float severity = Mathf.Pow(Mathf.Clamp(Mathf.Abs(angleDegrees), 0f, 180f) / 180f,
                    Mathf.Max(1f, anglePower));
                float relief = Mathf.Clamp01(gentleTurnRelief) * Mathf.Clamp01(level / 100f);
                float loss = Mathf.Max(0f, severity * fullTurnLoss - (1f - severity) * relief);
                return Mathf.Clamp01(1f - loss);
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
