using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PVZ.Items;
using PVZ.Player;
using PVZ.Utils;

namespace PVZ.Delivery
{
    /// <summary>
    /// Represents the cargo delivery truck that arrives outside the front entrance:
    /// - Performs a natural vehicle approach down the street, swings the cab out,
    ///   and reverses into position along a mathematically tangent-aligned curved arc
    ///   with reverse beeping, air-brake hiss, and realistic suspension settling.
    /// - Opens rear doors asymmetrically and drops an extendable walking ramp down to the asphalt (-20 deg).
    /// - Spawns ordered boxes in an accessible two-column layout.
    /// - Once unloaded, warns the player with a 15s timer before departing.
    /// - If the player remains inside, the truck drives away with the player for 10s,
    ///   awards the "Безбилетник" Steam achievement, and teleports the player back into the PVZ.
    /// </summary>
    public class DeliveryTruck : MonoBehaviour
    {
        [Header("Truck Animation & Movement")]
        [Tooltip("Transform representing the cargo bed where boxes spawn.")]
        [SerializeField] private Transform _cargoBay;

        [Tooltip("Left rear door transform.")]
        [SerializeField] private Transform _leftDoor;

        [Tooltip("Right rear door transform.")]
        [SerializeField] private Transform _rightDoor;

        [Tooltip("Extendable walking ramp at the rear of cargo bed.")]
        [SerializeField] private Transform _ramp;

        [Header("Parking Coordinates")]
        [SerializeField] private Vector3 _parkPosition = new Vector3(-2.2f, 0f, -11.0f);
        [SerializeField] private Quaternion _parkRotation = Quaternion.Euler(0f, 180f, 0f);
        [SerializeField] private bool _hasCustomPark;

        [Header("State")]
        [SerializeField] private bool _isArrived;
        [SerializeField] private List<SupplyBox> _cargoSupplyBoxes = new List<SupplyBox>();
        [SerializeField] private bool _isDeparturePending;
        [SerializeField] private float _departureCountdown;
        [SerializeField] private bool _isDrivingWithPlayer;

        private Coroutine _activeRoutine;
        private AudioSource _audioSource;

        /// <summary>
        /// Fires when the truck finishes parking, opens doors and ramp, and cargo is ready.
        /// </summary>
        public event Action OnTruckArrived;

        /// <summary>
        /// Fires when the player has removed all boxes from the truck.
        /// </summary>
        public event Action OnTruckFullyUnloaded;

        /// <summary>
        /// Fires when the truck closes its doors, ramp, and departs.
        /// </summary>
        public event Action OnTruckDeparted;

        /// <summary>
        /// True if the truck is parked and open for cargo unloading.
        /// </summary>
        public bool IsArrived => _isArrived;

        /// <summary>
        /// Number of supply crates currently remaining in the truck.
        /// </summary>
        public int RemainingBoxCount => _cargoSupplyBoxes.Count;

        /// <summary>
        /// True if the truck is currently driving down the road with the player inside.
        /// </summary>
        public bool IsDrivingWithPlayer => _isDrivingWithPlayer;

        /// <summary>
        /// True if truck is counting down to departure after being unloaded.
        /// </summary>
        public bool IsDeparturePending => _isDeparturePending;

        /// <summary>
        /// True if the truck is active, arriving, parked, unloading, or departing.
        /// </summary>
        public bool IsTruckBusy => gameObject.activeSelf || _isArrived || _cargoSupplyBoxes.Count > 0 || _isDeparturePending || _isDrivingWithPlayer;

        private void Awake()
        {
            if (!_hasCustomPark)
            {
                _parkPosition = transform.position;
                _parkRotation = transform.rotation;
                _hasCustomPark = true;
            }

            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
            _audioSource.spatialBlend = 0.5f;
            _audioSource.playOnAwake = false;
        }

        /// <summary>
        /// Sets the parking destination coordinates.
        /// </summary>
        public void SetParkTransform(Vector3 pos, Quaternion rot)
        {
            _parkPosition = pos;
            _parkRotation = rot;
            _hasCustomPark = true;
        }

        /// <summary>
        /// Checks whether the player is physically inside the truck's cargo interior.
        /// </summary>
        public bool IsPlayerInsideCargo()
        {
            if (PlayerController.Instance == null)
            {
                return false;
            }

            Vector3 localPos = transform.InverseTransformPoint(PlayerController.Instance.transform.position);
            // Interior bounds of cargo bed: width [-1.4, 1.4], depth [-2.4, 2.3], height [0.0, 3.0]
            return Mathf.Abs(localPos.x) <= 1.4f && localPos.z >= -2.4f && localPos.z <= 2.3f && localPos.y >= 0f && localPos.y <= 3.0f;
        }

        private void Update()
        {
            if (!_isArrived)
            {
                return;
            }

            // Remove supply crate reference once picked up, moved away from cargo bed, or unpacked
            for (int i = _cargoSupplyBoxes.Count - 1; i >= 0; i--)
            {
                SupplyBox box = _cargoSupplyBoxes[i];
                if (box == null || box.IsCarried || box.IsUnpacked || box.transform.parent != _cargoBay)
                {
                    _cargoSupplyBoxes.RemoveAt(i);
                }
            }

            // When all supply crates are unloaded/unpacked, initiate 15s departure countdown
            if (_cargoSupplyBoxes.Count == 0 && !_isDeparturePending)
            {
                _isDeparturePending = true;
                _departureCountdown = 15.0f;
            }

            if (_isDeparturePending)
            {
                _departureCountdown -= Time.deltaTime;

                if (IsPlayerInsideCargo())
                {
                    DeliveryManager.Instance?.ShowNotification(
                        $"⚠ ВНИМАНИЕ: Фура скоро уедет! У вас есть {TimeFormatter.FormatMinutesSeconds(_departureCountdown)}, чтобы покинуть кузов!",
                        0.25f);
                }

                if (_departureCountdown <= 0f)
                {
                    _isDeparturePending = false;
                    bool playerRiding = IsPlayerInsideCargo();

                    if (_activeRoutine != null)
                    {
                        StopCoroutine(_activeRoutine);
                    }
                    _activeRoutine = StartCoroutine(DepartureSequence(playerRiding));
                }
            }
        }

        /// <summary>
        /// Triggers animated arrival: drives down street, reverses naturally into position, opens doors, deploys ramp, and spawns boxes.
        /// </summary>
        public void ArriveWithCargo(List<int> orderIds)
        {
            gameObject.SetActive(true);

            if (_activeRoutine != null)
            {
                StopCoroutine(_activeRoutine);
            }
            _activeRoutine = StartCoroutine(ArrivalSequence(orderIds));
        }

        private IEnumerator ArrivalSequence(List<int> orderIds)
        {
            _isArrived = false;
            _isDeparturePending = false;
            _isDrivingWithPlayer = false;
            _cargoSupplyBoxes.Clear();

            // 1. Initial State: Closed doors and folded ramp
            if (_leftDoor != null) _leftDoor.localRotation = Quaternion.identity;
            if (_rightDoor != null) _rightDoor.localRotation = Quaternion.identity;
            if (_ramp != null) _ramp.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // -------------------------------------------------------------
            // PHASE 1: Cruising approach along street & swing-out maneuver (3.2 seconds)
            // -------------------------------------------------------------
            // Start further down the street heading West (-90 deg)
            Vector3 p0 = new Vector3(24.0f, 0f, -15.0f);
            Vector3 p1 = new Vector3(5.0f, 0f, -15.0f);

            // Final swing-out pose: cab angled -125 deg South-West, rear pointed North-East
            Vector3 swingPos = new Vector3(-4.5f, 0f, -16.4f);
            Quaternion swingRot = Quaternion.Euler(0f, -125f, 0f);
            Vector3 swingForward = swingRot * Vector3.forward;
            Vector3 p2 = swingPos - swingForward * 5.0f;
            Vector3 p3 = swingPos;

            transform.position = p0;
            transform.rotation = Quaternion.Euler(0f, -90f, 0f);

            float approachDuration = 3.2f;
            float elapsed = 0f;

            while (elapsed < approachDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / approachDuration);
                // Ease out curve: cruising in quickly, smoothly slowing down as it swings
                float t = Mathf.SmoothStep(0f, 1f, progress);

                transform.position = EvaluateCubicBezier(p0, p1, p2, p3, t);

                Vector3 tangent = EvaluateCubicBezierTangent(p0, p1, p2, p3, t);
                Vector3 flatTangent = new Vector3(tangent.x, 0f, tangent.z);
                if (flatTangent.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.LookRotation(flatTangent.normalized, Vector3.up);
                }

                yield return null;
            }

            transform.position = swingPos;
            transform.rotation = swingRot;

            // -------------------------------------------------------------
            // PHASE 2: Gear shift pause & reverse lights (0.4 seconds)
            // -------------------------------------------------------------
            yield return new WaitForSeconds(0.4f);

            // -------------------------------------------------------------
            // PHASE 3: Natural Reverse Arc into entrance parking bay (3.5 seconds)
            // -------------------------------------------------------------
            // Initial motion vector in reverse is opposite of cab forward
            Vector3 r0 = swingPos;
            Vector3 initReverseMotionDir = -swingForward; // points North-East (+55 deg)
            Vector3 r1 = r0 + initReverseMotionDir * 3.5f;

            // Final motion vector entering the slot is due North (+Z)
            Vector3 finalReverseMotionDir = Vector3.forward;
            Vector3 r3 = _parkPosition;
            Vector3 r2 = r3 - finalReverseMotionDir * 3.0f;

            float reverseDuration = 3.5f;
            elapsed = 0f;
            float beepTimer = 0f;

            while (elapsed < reverseDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / reverseDuration);
                // Smooth acceleration and deceleration for backing up
                float t = Mathf.SmoothStep(0f, 1f, progress);

                transform.position = EvaluateCubicBezier(r0, r1, r2, r3, t);

                Vector3 tangent = EvaluateCubicBezierTangent(r0, r1, r2, r3, t);
                Vector3 flatTangent = new Vector3(tangent.x, 0f, tangent.z);
                if (flatTangent.sqrMagnitude > 0.001f)
                {
                    // Since the truck is reversing, cab forward is opposite to the motion tangent!
                    transform.rotation = Quaternion.LookRotation(-flatTangent.normalized, Vector3.up);
                }

                // Reverse beeper sound
                beepTimer -= Time.deltaTime;
                if (beepTimer <= 0f)
                {
                    PlayReverseBeep();
                    beepTimer = 0.85f;
                }

                yield return null;
            }

            transform.position = _parkPosition;
            transform.rotation = _parkRotation;

            // -------------------------------------------------------------
            // PHASE 4: Air-brake hiss & decaying suspension bounce (0.45 seconds)
            // -------------------------------------------------------------
            PlayAirBrakeHiss();

            float settleTime = 0.45f;
            float settleElapsed = 0f;

            while (settleElapsed < settleTime)
            {
                settleElapsed += Time.deltaTime;
                float settleProgress = settleElapsed / settleTime;
                // Realistic decaying chassis pitch bounce of heavy vehicle stopping
                float decay = 1.0f - settleProgress;
                float pitchOffset = Mathf.Sin(settleProgress * Mathf.PI * 2.5f) * decay * 1.1f;
                transform.rotation = _parkRotation * Quaternion.Euler(pitchOffset, 0f, 0f);
                yield return null;
            }
            transform.rotation = _parkRotation;

            yield return new WaitForSeconds(0.2f);

            // -------------------------------------------------------------
            // PHASE 5: Natural asymmetric door opening (0.75 seconds)
            // -------------------------------------------------------------
            float doorDuration = 0.75f;
            float doorElapsed = 0f;
            Quaternion leftTarget = Quaternion.Euler(0f, -95f, 0f);
            Quaternion rightTarget = Quaternion.Euler(0f, 95f, 0f);

            while (doorElapsed < doorDuration)
            {
                doorElapsed += Time.deltaTime;

                // Left door opens first (0.0s to 0.55s)
                float tLeft = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(doorElapsed / 0.55f));
                if (_leftDoor != null)
                {
                    _leftDoor.localRotation = Quaternion.Slerp(Quaternion.identity, leftTarget, tLeft);
                }

                // Right door follows with slight offset (0.15s to 0.70s)
                float tRight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((doorElapsed - 0.15f) / 0.55f));
                if (_rightDoor != null)
                {
                    _rightDoor.localRotation = Quaternion.Slerp(Quaternion.identity, rightTarget, tRight);
                }

                yield return null;
            }

            if (_leftDoor != null) _leftDoor.localRotation = leftTarget;
            if (_rightDoor != null) _rightDoor.localRotation = rightTarget;

            // -------------------------------------------------------------
            // PHASE 6: Ramp descends smoothly down to asphalt (-20 deg, 0.65 seconds)
            // -------------------------------------------------------------
            if (_ramp != null)
            {
                float rampDuration = 0.65f;
                float rampElapsed = 0f;
                Quaternion rampFolded = Quaternion.Euler(90f, 0f, 0f);
                Quaternion rampDeployed = Quaternion.Euler(-20f, 0f, 0f); // Slopes down to asphalt

                while (rampElapsed < rampDuration)
                {
                    rampElapsed += Time.deltaTime;
                    float progress = Mathf.Clamp01(rampElapsed / rampDuration);
                    // Gravity ease curve (accelerates down)
                    float tRamp = Mathf.Pow(progress, 1.7f);
                    _ramp.localRotation = Quaternion.Slerp(rampFolded, rampDeployed, tRamp);
                    yield return null;
                }
                _ramp.localRotation = rampDeployed;
            }

            // -------------------------------------------------------------
            // PHASE 7: Spawn sealed supply crates in accessible 2-column layout (5 products per crate)
            // -------------------------------------------------------------
            int crateCount = Mathf.Max(1, (orderIds.Count + 4) / 5);
            int columns = Mathf.Min(crateCount, 2);

            for (int c = 0; c < crateCount; c++)
            {
                int row = c / columns;
                int col = c % columns;

                float xPos = (col == 0) ? -0.55f : 0.55f;
                float zPos = (row * 0.95f) - 0.4f;
                float yPos = 0.35f;

                Vector3 localSpawnPos = new Vector3(xPos, yPos, zPos);
                Vector3 worldPos = _cargoBay != null ? _cargoBay.TransformPoint(localSpawnPos) : transform.position + localSpawnPos;
                Quaternion spawnRot = _cargoBay != null ? _cargoBay.rotation : transform.rotation;

                // Gather 5 product IDs for this crate
                var crateIds = new List<int>();
                int startIdx = c * 5;
                for (int j = 0; j < 5; j++)
                {
                    int idx = startIdx + j;
                    if (idx < orderIds.Count)
                    {
                        crateIds.Add(orderIds[idx]);
                    }
                    else
                    {
                        crateIds.Add(orderIds.Count > 0 ? (orderIds[0] + idx) : (200 + idx));
                    }
                }

                GameObject crateObj = new GameObject($"SupplyCrate_{c + 1}");
                crateObj.transform.position = worldPos;
                crateObj.transform.rotation = spawnRot;
                if (_cargoBay != null)
                {
                    crateObj.transform.SetParent(_cargoBay);
                }

                SupplyBox supplyBox = crateObj.AddComponent<SupplyBox>();
                supplyBox.Initialize(crateIds);

                _cargoSupplyBoxes.Add(supplyBox);
            }

            _isArrived = true;
            OnTruckArrived?.Invoke();
        }

        private IEnumerator DepartureSequence(bool playerRiding)
        {
            _isArrived = false;
            OnTruckFullyUnloaded?.Invoke();

            // 1. Ramp retracts / folds back up
            if (_ramp != null)
            {
                float rampTime = 0.55f;
                float elapsedRamp = 0f;
                Quaternion rampDeployed = Quaternion.Euler(-20f, 0f, 0f);
                Quaternion rampFolded = Quaternion.Euler(90f, 0f, 0f);

                while (elapsedRamp < rampTime)
                {
                    elapsedRamp += Time.deltaTime;
                    float t = Mathf.SmoothStep(0f, 1f, elapsedRamp / rampTime);
                    _ramp.localRotation = Quaternion.Slerp(rampDeployed, rampFolded, t);
                    yield return null;
                }
                _ramp.localRotation = rampFolded;
            }

            // 2. Asymmetric door closing (right door closes first, left latches over it)
            float doorTime = 0.75f;
            float elapsedDoor = 0f;
            Quaternion leftOpen = (_leftDoor != null) ? _leftDoor.localRotation : Quaternion.identity;
            Quaternion rightOpen = (_rightDoor != null) ? _rightDoor.localRotation : Quaternion.identity;

            while (elapsedDoor < doorTime)
            {
                elapsedDoor += Time.deltaTime;

                // Right door closes first (0.0s to 0.55s)
                float tRight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsedDoor / 0.55f));
                if (_rightDoor != null)
                {
                    _rightDoor.localRotation = Quaternion.Slerp(rightOpen, Quaternion.identity, tRight);
                }

                // Left door follows (0.15s to 0.70s)
                float tLeft = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsedDoor - 0.15f) / 0.55f));
                if (_leftDoor != null)
                {
                    _leftDoor.localRotation = Quaternion.Slerp(leftOpen, Quaternion.identity, tLeft);
                }

                yield return null;
            }

            if (_rightDoor != null) _rightDoor.localRotation = Quaternion.identity;
            if (_leftDoor != null) _leftDoor.localRotation = Quaternion.identity;

            yield return new WaitForSeconds(0.3f);

            // Natural Departure: Curves forward out of bay down to Z = -15.5, then turns West down the street
            Vector3 d0 = _parkPosition;
            Vector3 d1 = new Vector3(_parkPosition.x, 0f, -14.5f);
            Vector3 d2 = new Vector3(-8.0f, 0f, -15.5f);
            Vector3 d3 = new Vector3(-38.0f, 0f, -15.5f);

            if (!playerRiding)
            {
                float driveDuration = 3.6f;
                float elapsed = 0f;

                while (elapsed < driveDuration)
                {
                    elapsed += Time.deltaTime;
                    float progress = Mathf.Clamp01(elapsed / driveDuration);
                    float t = Mathf.SmoothStep(0f, 1f, progress);

                    transform.position = EvaluateCubicBezier(d0, d1, d2, d3, t);

                    Vector3 tangent = EvaluateCubicBezierTangent(d0, d1, d2, d3, t);
                    Vector3 flatTangent = new Vector3(tangent.x, 0f, tangent.z);
                    if (flatTangent.sqrMagnitude > 0.001f)
                    {
                        transform.rotation = Quaternion.LookRotation(flatTangent.normalized, Vector3.up);
                    }

                    yield return null;
                }

                // Reset and disable for next shipment
                gameObject.SetActive(false);
                transform.position = _parkPosition;
                transform.rotation = _parkRotation;
                OnTruckDeparted?.Invoke();
            }
            else
            {
                // EASTER EGG: Player is riding in the truck!
                _isDrivingWithPlayer = true;
                float rideDuration = 10.0f;
                float elapsed = 0f;
                Vector3 prevTruckPos = transform.position;

                while (elapsed < rideDuration)
                {
                    elapsed += Time.deltaTime;

                    // Progress through departure curve then continue straight West
                    if (elapsed <= 3.6f)
                    {
                        float t = Mathf.SmoothStep(0f, 1f, elapsed / 3.6f);
                        transform.position = EvaluateCubicBezier(d0, d1, d2, d3, t);

                        Vector3 tangent = EvaluateCubicBezierTangent(d0, d1, d2, d3, t);
                        Vector3 flatTangent = new Vector3(tangent.x, 0f, tangent.z);
                        if (flatTangent.sqrMagnitude > 0.001f)
                        {
                            transform.rotation = Quaternion.LookRotation(flatTangent.normalized, Vector3.up);
                        }
                    }
                    else
                    {
                        // Cruising West down the road
                        transform.position += Vector3.left * (12.0f * Time.deltaTime);
                    }

                    // Move player CharacterController along with the truck
                    Vector3 delta = transform.position - prevTruckPos;
                    if (PlayerController.Instance != null)
                    {
                        var cc = PlayerController.Instance.GetComponent<CharacterController>();
                        if (cc != null && cc.enabled)
                        {
                            cc.Move(delta);
                        }
                    }

                    prevTruckPos = transform.position;
                    yield return null;
                }

                _isDrivingWithPlayer = false;

                // Unlock Steam Achievement!
                if (DeliveryManager.Instance != null)
                {
                    DeliveryManager.Instance.TriggerSteamAchievement("«Безбилетник»", "Вы случайно уехали на склад в кузове фуры");
                    DeliveryManager.Instance.ShowNotification("📦 Вас высадили на обочине, и вы вернулись обратно в свой ПВЗ!", 6.0f);
                }

                // Teleport player safely back into the PVZ room
                if (PlayerController.Instance != null)
                {
                    PlayerController.Instance.TeleportTo(new Vector3(-0.5f, 1.0f, 3.6f), Quaternion.Euler(0f, 180f, 0f));
                }

                // Despawn truck and reset
                gameObject.SetActive(false);
                transform.position = _parkPosition;
                transform.rotation = _parkRotation;
                OnTruckDeparted?.Invoke();
            }
        }

        private void PlayAirBrakeHiss()
        {
            if (_audioSource == null) return;

            int sampleRate = 44100;
            int length = (int)(sampleRate * 0.45f);
            float[] data = new float[length];

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 6.5f);
                float noise = (UnityEngine.Random.value * 2f - 1f) * 0.32f;
                data[i] = noise * env;
            }

            AudioClip clip = AudioClip.Create("TruckAirBrake", length, 1, sampleRate, false);
            clip.SetData(data, 0);
            _audioSource.PlayOneShot(clip, 0.55f);
        }

        private void PlayReverseBeep()
        {
            if (_audioSource == null) return;

            int sampleRate = 44100;
            int length = (int)(sampleRate * 0.12f);
            float[] data = new float[length];

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Clamp01(1f - (t / 0.12f));
                data[i] = Mathf.Sin(2f * Mathf.PI * 1050f * t) * env * 0.22f;
            }

            AudioClip clip = AudioClip.Create("ReverseBeep", length, 1, sampleRate, false);
            clip.SetData(data, 0);
            _audioSource.PlayOneShot(clip, 0.35f);
        }

        private static Vector3 EvaluateCubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float u = 1f - t;
            return (u * u * u * p0) + (3f * u * u * t * p1) + (3f * u * t * t * p2) + (t * t * t * p3);
        }

        private static Vector3 EvaluateCubicBezierTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float u = 1f - t;
            return (3f * u * u * (p1 - p0)) + (6f * u * t * (p2 - p1)) + (3f * t * t * (p3 - p2));
        }

        private void OnGUI()
        {
            if (_isDrivingWithPlayer)
            {
                // Mood overlay while locked inside moving truck
                GUIStyle darkStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 18,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                darkStyle.normal.textColor = Color.yellow;

                GUI.Box(new Rect((Screen.width - 600) / 2, Screen.height * 0.4f, 600, 50),
                    "🚛 Вы едете в закрытом кузове фуры в неизвестном направлении...", darkStyle);
            }
        }
    }
}
