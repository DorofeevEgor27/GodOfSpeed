using System;
using UnityEngine;

namespace GodOfSpeed.Racing
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CarCharacteristics))]
    [AddComponentMenu("SpeedGod/Топливный бак")]
    public sealed class CarFuelTank : MonoBehaviour
    {
        private CarCharacteristics characteristics;
        private double currentFuel;

        private bool HasInfiniteFuel => characteristics != null
            && characteristics.HasConfig && characteristics.InfiniteFuel;

        public double CurrentFuel => HasInfiniteFuel ? characteristics.FuelCapacity : currentFuel;
        public bool IsEmpty => !HasInfiniteFuel && currentFuel <= 0d;

        private void Awake()
        {
            characteristics = GetComponent<CarCharacteristics>();
            FillToCapacity();
        }

        public void FillToCapacity()
        {
            currentFuel = characteristics != null && characteristics.HasConfig
                ? characteristics.FuelCapacity : 0d;
        }

        // Fuel uses game units: 20 km/h consumes 20 units per second, not per hour.
        // Called once per physics step by the driver after the race has started.
        public void Consume(double speedKmh, double deltaSeconds)
        {
            if (double.IsNaN(speedKmh) || double.IsInfinity(speedKmh) || speedKmh < 0d)
                throw new ArgumentOutOfRangeException(nameof(speedKmh));
            if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

            if (HasInfiniteFuel)
            {
                FillToCapacity();
                return;
            }

            currentFuel = Math.Max(0d, currentFuel - speedKmh * deltaSeconds);
        }
    }
}
