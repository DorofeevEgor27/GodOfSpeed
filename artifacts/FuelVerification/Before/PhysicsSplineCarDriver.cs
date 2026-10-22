using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.Splines;

namespace GodOfSpeed.Racing
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PhysicsSplineCarDriver : MonoBehaviour
    {
        private const float MaxSteerAngle = 18f;
        private const float LateralAccelerationLimit = 4f;
        private const float BrakingAcceleration = 6f;
        private const float PlannedBrakingAcceleration = 2f;
        private const float AccelerationChangeRate = 10f;
        private const float SteerSpeed = 45f;
        private const float MinWheelRadius = 0.01f;
        private const float DefaultBodyMass = 150f;
        private const float CloseTargetDistance = 0.5f;
        private const float WheelTransformRotationTolerance = 1f;
        private const float WheelTransformScaleTolerance = 0.01f;

        [SerializeField, Tooltip("Show this car's actual speed on the HUD. Mark only one car as Main Car.")] private bool mainCar;
        [SerializeField] private SplineContainer spline;
        [SerializeField, Tooltip("Optional scene object to drive towards instead of the automatic spline point. Move this object in the Scene view during Play Mode.")] private GameObject targetObject;
        [SerializeField, Min(0.1f), Tooltip("Distance from the front wheels at which the car stops near Target Object.")] private float stoppingDistance = CloseTargetDistance;
        // Preserve legacy serialized settings for cars without the new characteristics component.
        [SerializeField, HideInInspector, FormerlySerializedAs("targetPointSpeedKmh"), FormerlySerializedAs("targetSpeedKmh")] private float speedKmh = 20f;
        [SerializeField, HideInInspector, FormerlySerializedAs("engineAcceleration"), FormerlySerializedAs("acceleration")] private float acceleration = 12f;
        [SerializeField] private WheelCollider frontLeftWheel;
        [SerializeField] private WheelCollider frontRightWheel;
        [SerializeField] private Transform targetPointVisual;
        [SerializeField] private bool loop = true;

        private Rigidbody body;
        private CarCharacteristics characteristics;
        private float splineLength;
        private float progress;
        private float carSplineProgress;
        private float[] splineDistances;
        private float currentAllowedSpeedMps;
        private Vector3 targetPoint;
        private float currentSteerAngle;
        private bool isRunning;
        private bool warnedAboutGround;
        private float timeWithoutGround;
        private WheelCollider[] physicsWheels;
        private float wheelbase = 1f;
        private float driveAcceleration;
        private bool hasArrived;
        private Vector3 arrivalPoint;

        public bool IsMainCar => mainCar;
        public float CurrentSpeedKmh => body != null ? body.linearVelocity.magnitude * 3.6f : 0f;

        private bool HasCharacteristics => characteristics != null && characteristics.HasConfig;
        private float MaximumSpeedKmh => HasCharacteristics ? characteristics.SpeedKmh : speedKmh;
        private float DriveAcceleration => HasCharacteristics ? characteristics.Acceleration : acceleration;
        private float CorneringAccelerationLimit => HasCharacteristics ? characteristics.CorneringAcceleration : LateralAccelerationLimit;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            characteristics = GetComponent<CarCharacteristics>();
            body.useGravity = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            body.centerOfMass = new Vector3(0f, -0.25f, 0f);
            body.mass = Mathf.Max(body.mass, DefaultBodyMass);
            body.angularDamping = Mathf.Max(body.angularDamping, 2f);

            AssignMissingWheelsFromChildren();

            if (!HasFrontWheels())
            {
                Debug.LogWarning("Assign Front Left Wheel and Front Right Wheel, or add two child WheelColliders to this car. WheelColliders must stand on the road, otherwise suspension can launch the car.", this);
                enabled = false;
                return;
            }

            frontLeftWheel = PrepareWheelCollider(frontLeftWheel, "Front Left Physics Wheel");
            frontRightWheel = PrepareWheelCollider(frontRightWheel, "Front Right Physics Wheel");

            physicsWheels = System.Array.FindAll(GetComponentsInChildren<WheelCollider>(),
                wheel => wheel.enabled && wheel.attachedRigidbody == body);
            float frontZ = transform.InverseTransformPoint(GetSteeringOrigin()).z;
            float rearZ = frontZ;
            foreach (WheelCollider wheel in physicsWheels)
            {
                rearZ = Mathf.Min(rearZ, transform.InverseTransformPoint(wheel.transform.position).z);
                ConfigureWheel(wheel);
                wheel.ConfigureVehicleSubsteps(5f, 12, 15);
            }
            wheelbase = Mathf.Max(0.1f, (frontZ - rearZ) * Mathf.Abs(transform.lossyScale.z));

            if (targetObject != null)
            {
                targetPoint = targetObject.transform.position;
                return;
            }

            RefreshSplineLength();
            if (splineLength <= 0f)
            {
                return;
            }

            progress = FindNearestSplinePosition();
            currentAllowedSpeedMps = GetBaseTargetSpeedMps();
            UpdateTargetPoint();
        }

        private void Update()
        {
            if (isRunning)
            {
                return;
            }

            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                isRunning = true;
            }
        }

        private void FixedUpdate()
        {
            if (!HasFrontWheels())
            {
                StopWheels();
                return;
            }

            if (targetObject != null)
            {
                targetPoint = targetObject.transform.position;
                if (!isRunning || targetObject.transform.IsChildOf(transform))
                {
                    BrakeWheels();
                    return;
                }

                UpdateObjectTargetSpeed();
                SteerWheels();
                DriveWheels();
                WarnIfWheelsHaveNoGround();
                return;
            }

            if (spline == null || spline.Spline == null || spline.Spline.Count < 2)
            {
                BrakeWheels();
                return;
            }

            if (splineLength <= 0f)
            {
                RefreshSplineLength();
            }

            if (splineLength <= 0f)
            {
                StopWheels();
                return;
            }

            UpdateTargetPointVisual();

            if (!isRunning)
            {
                BrakeWheels();
                return;
            }

            AdvanceTargetPoint();
            UpdateTargetPoint();
            UpdateAllowedSpeed();
            SteerWheels();
            DriveWheels();
            UpdateTargetPointVisual();
            WarnIfWheelsHaveNoGround();
        }

        private void OnValidate()
        {
            splineLength = 0f;
        }

        private void RefreshSplineLength()
        {
            splineLength = spline == null || spline.Spline == null || spline.Spline.Count < 2
                ? 0f
                : spline.CalculateLength();
            if (splineLength <= 0f)
            {
                splineDistances = null;
                return;
            }
            // Parameter t is not distance: sample world-space arc length once.
            int segments = Mathf.Clamp(Mathf.CeilToInt(splineLength * 4f), 128, 8192);
            splineDistances = new float[segments + 1];
            Vector3 previous = ToVector3(spline.EvaluatePosition(0f));
            for (int i = 1; i <= segments; i++)
            {
                Vector3 position = ToVector3(spline.EvaluatePosition((float)i / segments));
                splineDistances[i] = splineDistances[i - 1] + Vector3.Distance(previous, position);
                previous = position;
            }
            splineLength = splineDistances[segments];
        }

        private void AdvanceTargetPoint()
        {
            carSplineProgress = FindNearestSplinePosition();
            float speed = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).magnitude;
            float leadDistance = Mathf.Max(wheelbase * 2f, speed * 0.8f);
            progress = GetProgressAtDistance(carSplineProgress, leadDistance);
        }

        private float GetProgressAtDistance(float fromProgress, float distance)
        {
            int segments = splineDistances.Length - 1;
            float startDistance = GetDistanceAtProgress(fromProgress);
            float wanted = ShouldLoop() ? Mathf.Repeat(startDistance + distance, splineLength) : Mathf.Clamp(startDistance + distance, 0f, splineLength);
            int upper = System.Array.BinarySearch(splineDistances, wanted);
            if (upper >= 0) return (float)upper / segments;
            upper = Mathf.Clamp(~upper, 1, segments);
            float fraction = Mathf.InverseLerp(splineDistances[upper - 1], splineDistances[upper], wanted);
            return (upper - 1 + fraction) / segments;
        }

        private float GetDistanceAtProgress(float splineProgress)
        {
            int segments = splineDistances.Length - 1;
            float index = Mathf.Clamp01(splineProgress) * segments;
            int lower = Mathf.Min(Mathf.FloorToInt(index), segments - 1);
            return Mathf.Lerp(splineDistances[lower], splineDistances[lower + 1], index - lower);
        }

        private void UpdateTargetPoint()
        {
            targetPoint = ToVector3(spline.EvaluatePosition(progress));
        }

        private void UpdateAllowedSpeed()
        {
            currentAllowedSpeedMps = GetBaseTargetSpeedMps();
            if (!ShouldLoop())
            {
                float speed = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).magnitude;
                float remaining = Mathf.Max(0f, splineLength - GetDistanceAtProgress(carSplineProgress) - stoppingDistance - speed * Time.fixedDeltaTime * 2f);
                currentAllowedSpeedMps = Mathf.Min(currentAllowedSpeedMps, Mathf.Sqrt(2f * PlannedBrakingAcceleration * remaining), remaining * 1.5f);
            }
        }

        private float GetBaseTargetSpeedMps()
        {
            return Mathf.Max(0f, MaximumSpeedKmh) / 3.6f;
        }

        private float GetAllowedTargetSpeedMps()
        {
            return Mathf.Max(0f, currentAllowedSpeedMps);
        }

        private void UpdateObjectTargetSpeed()
        {
            Vector3 offset = targetPoint - GetSteeringOrigin();
            offset.y = 0f;
            if (hasArrived && Vector3.ProjectOnPlane(targetPoint - arrivalPoint, Vector3.up).magnitude > stoppingDistance)
                hasArrived = false;
            if (offset.magnitude <= stoppingDistance)
            {
                hasArrived = true;
                arrivalPoint = targetPoint;
            }
            float speed = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).magnitude;
            float remainingDistance = Mathf.Max(0f, offset.magnitude - stoppingDistance - speed * Time.fixedDeltaTime * 2f);
            float arrivalSpeed = Mathf.Min(Mathf.Sqrt(2f * PlannedBrakingAcceleration * remainingDistance), remainingDistance * 1.5f);
            currentAllowedSpeedMps = hasArrived ? 0f : Mathf.Min(GetBaseTargetSpeedMps(), arrivalSpeed);
        }

        private void BrakeWheels()
        {
            driveAcceleration = 0f;
            ApplyWheelForces(0f, BrakingAcceleration);
        }

        private void SteerWheels()
        {
            Vector3 targetDirection = GetTargetDirection();
            if (targetDirection.sqrMagnitude <= 0.0001f)
            {
                SetSteering(0f);
                return;
            }

            Vector3 forward = transform.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            float angle = Vector3.SignedAngle(forward.normalized, targetDirection, Vector3.up);
            float speed = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).magnitude;
            float distance = Vector3.ProjectOnPlane(targetPoint - GetSteeringOrigin(), Vector3.up).magnitude;
            float lookAhead = Mathf.Max(wheelbase * 2f, Mathf.Min(distance, Mathf.Max(wheelbase * 2f, speed * 0.8f)));
            float curvature = 2f * Mathf.Sin(Mathf.Clamp(angle, -90f, 90f) * Mathf.Deg2Rad) / lookAhead;
            float requestedSteerAngle = Mathf.Clamp(Mathf.Atan(wheelbase * curvature) * Mathf.Rad2Deg, -MaxSteerAngle, MaxSteerAngle);
            // Plan speed from the requested wheel angle BEFORE limiting the actual
            // steering. The wheels can turn further as braking reduces speed.
            float turningSpeed = GetSafeSpeedForSteering(requestedSteerAngle);
            if (Mathf.Abs(angle) > 90f) turningSpeed = Mathf.Min(turningSpeed, 2f);
            currentAllowedSpeedMps = Mathf.Min(currentAllowedSpeedMps, turningSpeed);
            curvature = Mathf.Tan(requestedSteerAngle * Mathf.Deg2Rad) / wheelbase;
            float corneringLimit = CorneringAccelerationLimit;
            float limitedCurvature = Mathf.Clamp(curvature, -corneringLimit / Mathf.Max(1f, speed * speed), corneringLimit / Mathf.Max(1f, speed * speed));
            float desiredYawRate = speed * limitedCurvature;
            float steerAngle = Mathf.Atan(wheelbase * limitedCurvature) * Mathf.Rad2Deg;
            // Feedback damps rotation that the physical chassis has already acquired.
            steerAngle += (desiredYawRate - body.angularVelocity.y) * Mathf.Rad2Deg * 0.25f;
            SetSteering(Mathf.Clamp(steerAngle, -GetSpeedLimitedMaxSteerAngle(), GetSpeedLimitedMaxSteerAngle()));
        }

        private float GetSafeSpeedForSteering(float wheelAngle)
        {
            float curvature = Mathf.Abs(Mathf.Tan(wheelAngle * Mathf.Deg2Rad)) / wheelbase;
            return Mathf.Sqrt(CorneringAccelerationLimit / Mathf.Max(0.0001f, curvature));
        }

        private void SetSteering(float angle)
        {
            currentSteerAngle = Mathf.MoveTowards(currentSteerAngle, angle, SteerSpeed * Time.fixedDeltaTime);
            // A changing speed limit must take effect immediately, including during acceleration.
            currentSteerAngle = Mathf.Clamp(currentSteerAngle, -GetSpeedLimitedMaxSteerAngle(), GetSpeedLimitedMaxSteerAngle());
            frontLeftWheel.steerAngle = currentSteerAngle;
            frontRightWheel.steerAngle = currentSteerAngle;
        }

        private float GetSpeedLimitedMaxSteerAngle()
        {
            Vector3 horizontalVelocity = body.linearVelocity;
            horizontalVelocity.y = 0f;

            float safeAngle = Mathf.Atan(CorneringAccelerationLimit * wheelbase / Mathf.Max(1f, horizontalVelocity.sqrMagnitude)) * Mathf.Rad2Deg;
            return Mathf.Min(MaxSteerAngle, safeAngle);
        }

        private void DriveWheels()
        {
            float targetSpeed = GetAllowedTargetSpeedMps();
            float currentSpeed = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).magnitude;
            float speedError = targetSpeed - currentSpeed;
            float requestedAcceleration = Mathf.Min(Mathf.Max(0f, DriveAcceleration), Mathf.Max(0f, speedError * 2f));
            if (targetSpeed <= 0f || speedError < -0.1f)
            {
                driveAcceleration = 0f;
                ApplyWheelForces(0f, targetSpeed <= 0f ? BrakingAcceleration : Mathf.Min(BrakingAcceleration, -speedError * 3f));
                return;
            }
            driveAcceleration = Mathf.MoveTowards(driveAcceleration, requestedAcceleration, AccelerationChangeRate * Time.fixedDeltaTime);
            ApplyWheelForces(driveAcceleration, 0f);
        }

        private void ApplyWheelForces(float requestedAcceleration, float requestedBraking)
        {
            if (physicsWheels == null) return;
            float axleForce = Mathf.Min(body.mass * requestedAcceleration * 0.5f,
                Mathf.Min(GetWheelForceLimit(frontLeftWheel), GetWheelForceLimit(frontRightWheel)));
            foreach (WheelCollider wheel in physicsWheels)
            {
                wheel.motorTorque = 0f;
                wheel.brakeTorque = 0f;
                if (!wheel.GetGroundHit(out WheelHit hit)) continue;
                float radius = GetWheelRadius(wheel);
                if (requestedBraking > 0f)
                    wheel.brakeTorque = Mathf.Min(body.mass * requestedBraking / physicsWheels.Length, GetWheelForceLimit(wheel)) * radius;
                else if (wheel == frontLeftWheel || wheel == frontRightWheel)
                    wheel.motorTorque = axleForce * radius;
            }
        }

        private static float GetWheelForceLimit(WheelCollider wheel)
        {
            if (!wheel.GetGroundHit(out WheelHit hit)) return 0f;
            float grip = Mathf.Max(0f, wheel.forwardFriction.extremumValue * wheel.forwardFriction.stiffness);
            float slipLimit = Mathf.Max(0.01f, wheel.forwardFriction.extremumSlip * 0.7f);
            float slipScale = Mathf.Clamp01(slipLimit / Mathf.Max(slipLimit, Mathf.Abs(hit.forwardSlip)));
            return Mathf.Max(0f, hit.force) * grip * 0.55f * slipScale;
        }

        private static float GetWheelRadius(WheelCollider wheel)
        {
            return Mathf.Max(MinWheelRadius, wheel.radius * Mathf.Abs(wheel.transform.lossyScale.y));
        }

        private void StopWheels()
        {
            if (physicsWheels != null)
                foreach (WheelCollider wheel in physicsWheels) ResetWheel(wheel);
            ResetWheel(frontLeftWheel);
            ResetWheel(frontRightWheel);
            currentSteerAngle = 0f;
            driveAcceleration = 0f;
        }

        private static void ResetWheel(WheelCollider wheel)
        {
            if (wheel == null)
            {
                return;
            }

            wheel.motorTorque = 0f;
            wheel.brakeTorque = 0f;
            wheel.steerAngle = 0f;
        }

        private void OnDisable()
        {
            StopWheels();
        }

        private Vector3 GetTargetDirection()
        {
            Vector3 steeringOrigin = GetSteeringOrigin();
            Vector3 toTarget = targetPoint - steeringOrigin;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude <= CloseTargetDistance * CloseTargetDistance)
            {
                if (targetObject != null)
                {
                    return Vector3.zero;
                }

                Vector3 tangent = ToVector3(spline.EvaluateTangent(progress));
                tangent.y = 0f;
                return tangent.normalized;
            }

            return toTarget.normalized;
        }

        private float FindNearestSplinePosition()
        {
            if (spline.Spline == null)
            {
                return 0f;
            }

            float3 localPosition = spline.transform.InverseTransformPoint(GetSteeringOrigin());
            SplineUtility.GetNearestPoint(spline.Spline, localPosition, out _, out float nearestT);
            return nearestT;
        }

        private void ConfigureWheel(WheelCollider wheel)
        {
            // Scale rotational damping with wheel inertia instead of a fixed value
            // that makes small wheels consume almost all available drive torque.
            float radius = GetWheelRadius(wheel);
            wheel.wheelDampingRate = 0.05f * wheel.mass * radius * radius;
            JointSpring suspension = wheel.suspensionSpring;
            float supportedMass = Mathf.Max(1f, wheel.sprungMass);
            suspension.targetPosition = 0.5f;
            float suspensionTravel = wheel.suspensionDistance * Mathf.Abs(wheel.transform.lossyScale.y);
            suspension.spring = supportedMass * Physics.gravity.magnitude / Mathf.Max(0.025f, suspensionTravel * 0.5f);
            suspension.damper = 1.6f * Mathf.Sqrt(suspension.spring * supportedMass);
            wheel.suspensionSpring = suspension;

            WheelFrictionCurve forwardFriction = wheel.forwardFriction;
            forwardFriction.stiffness = 1.5f;
            wheel.forwardFriction = forwardFriction;

            WheelFrictionCurve sidewaysFriction = wheel.sidewaysFriction;
            sidewaysFriction.stiffness = 2f;
            wheel.sidewaysFriction = sidewaysFriction;
        }

        private WheelCollider PrepareWheelCollider(WheelCollider source, string wheelName)
        {
            if (source == null || IsCleanPhysicsWheel(source.transform))
            {
                return source;
            }

            Vector3 wheelPosition = source.transform.position;
            source.enabled = false;

            GameObject wheelObject = new GameObject(wheelName);
            wheelObject.transform.SetParent(transform, false);
            wheelObject.transform.localPosition = transform.InverseTransformPoint(wheelPosition);
            wheelObject.transform.localRotation = Quaternion.identity;
            wheelObject.transform.localScale = Vector3.one;

            WheelCollider wheel = wheelObject.AddComponent<WheelCollider>();
            wheelObject.layer = source.gameObject.layer;
            wheel.radius = source.radius;
            wheel.center = wheelObject.transform.InverseTransformPoint(source.transform.TransformPoint(source.center));
            wheel.mass = source.mass;
            wheel.wheelDampingRate = source.wheelDampingRate;
            wheel.suspensionDistance = source.suspensionDistance;
            wheel.forceAppPointDistance = source.forceAppPointDistance;
            wheel.suspensionSpring = source.suspensionSpring;
            wheel.forwardFriction = source.forwardFriction;
            wheel.sidewaysFriction = source.sidewaysFriction;
            Debug.LogWarning($"WheelCollider was assigned on visual object '{source.name}', which has rotation or scale unsuitable for WheelCollider physics. Created a clean runtime wheel holder '{wheelName}' at the same position.", this);
            return wheel;
        }

        private static bool IsCleanPhysicsWheel(Transform wheelTransform)
        {
            bool hasCleanRotation = Quaternion.Angle(wheelTransform.localRotation, Quaternion.identity) <= WheelTransformRotationTolerance;
            bool hasCleanScale = Vector3.Distance(wheelTransform.localScale, Vector3.one) <= WheelTransformScaleTolerance;
            return hasCleanRotation && hasCleanScale;
        }

        private void AssignMissingWheelsFromChildren()
        {
            if (frontLeftWheel != null && frontRightWheel != null)
            {
                return;
            }

            WheelCollider[] wheels = GetComponentsInChildren<WheelCollider>(true);
            for (int i = 0; i < wheels.Length; i++)
            {
                if (wheels[i] == frontLeftWheel || wheels[i] == frontRightWheel || wheels[i].attachedRigidbody != body)
                {
                    continue;
                }

                if (frontLeftWheel == null)
                {
                    frontLeftWheel = wheels[i];
                    continue;
                }

                if (frontRightWheel == null && wheels[i] != frontLeftWheel)
                {
                    frontRightWheel = wheels[i];
                    return;
                }
            }
        }

        private bool HasFrontWheels()
        {
            return frontLeftWheel != null && frontRightWheel != null && frontLeftWheel != frontRightWheel
                && frontLeftWheel.attachedRigidbody == body && frontRightWheel.attachedRigidbody == body;
        }

        private Vector3 GetSteeringOrigin()
        {
            if (frontLeftWheel == null || frontRightWheel == null)
            {
                return transform.position;
            }

            return (frontLeftWheel.transform.position + frontRightWheel.transform.position) * 0.5f;
        }

        private void UpdateTargetPointVisual()
        {
            if (targetPointVisual == null || targetObject != null)
            {
                return;
            }

            targetPointVisual.position = targetPoint;
        }

        private void WarnIfWheelsHaveNoGround()
        {
            if (frontLeftWheel.isGrounded || frontRightWheel.isGrounded)
            {
                timeWithoutGround = 0f;
                return;
            }

            timeWithoutGround += Time.fixedDeltaTime;
            if (warnedAboutGround || timeWithoutGround < 0.5f) return;
            warnedAboutGround = true;
            Debug.LogWarning("Front wheel colliders have had no ground contact for half a second. Check the road collider and wheel placement if the car is not intentionally airborne.", this);
        }

        private bool ShouldLoop()
        {
            return loop && spline.Spline != null && spline.Spline.Closed;
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            Gizmos.color = Color.cyan;
            Vector3 steeringOrigin = GetSteeringOrigin();
            Gizmos.DrawSphere(targetPoint, 0.35f);
            Gizmos.DrawLine(steeringOrigin, targetPoint);
        }
    }
}
