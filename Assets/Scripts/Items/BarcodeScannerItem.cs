using System;
using System.Collections;
using UnityEngine;
using PVZ.Customers;
using PVZ.Warehouse;

namespace PVZ.Items
{
    /// <summary>
    /// Interactive physical handheld barcode scanner.
    /// Can be picked up from the counter, carried in hands, used to scan customers' phones
    /// and incoming delivery boxes, and placed back onto the counter stand or surfaces.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(BoxCollider))]
    public class BarcodeScannerItem : MonoBehaviour
    {
        [Header("Scanner Properties")]
        [Tooltip("Effective barcode scan distance in meters.")]
        [SerializeField] private float _scanRange = 4.5f;

        [Tooltip("Transform representing the scanner cradle/stand on the checkout counter.")]
        [SerializeField] private Transform _cradleTransform;

        private Rigidbody _rigidbody;
        private Collider _collider;
        private AudioSource _audioSource;
        private LineRenderer _laserLine;
        private Light _scanFlashLight;

        private bool _isCarried;
        private Coroutine _laserCoroutine;
        private static AudioClip _beepClip;

        /// <summary>
        /// True when the scanner is currently held in player hands.
        /// </summary>
        public bool IsCarried => _isCarried;

        /// <summary>
        /// Reference to the counter cradle transform.
        /// </summary>
        public Transform CradleTransform
        {
            get => _cradleTransform;
            set => _cradleTransform = value;
        }

        private void Awake()
        {
            EnsureComponents();
            BuildVisualsIfNeeded();
        }

        private void EnsureComponents()
        {
            if (_rigidbody == null)
            {
                _rigidbody = GetComponent<Rigidbody>();
            }
            if (_collider == null)
            {
                _collider = GetComponent<Collider>();
                if (_collider == null)
                {
                    _collider = gameObject.AddComponent<BoxCollider>();
                }
            }

            if (_audioSource == null)
            {
                _audioSource = gameObject.GetComponent<AudioSource>();
                if (_audioSource == null)
                {
                    _audioSource = gameObject.AddComponent<AudioSource>();
                }
                _audioSource.playOnAwake = false;
                _audioSource.spatialBlend = 0.5f;
            }

            if (_beepClip == null)
            {
                _beepClip = CreateBeepClip(2400f, 0.09f);
            }
        }

        /// <summary>
        /// Generates a realistic low-poly handheld barcode scanner mesh and laser line if absent.
        /// </summary>
        private void BuildVisualsIfNeeded()
        {
            // Check if model already exists
            if (transform.Find("ScannerModel") != null)
            {
                return;
            }

            GameObject modelRoot = new GameObject("ScannerModel");
            modelRoot.transform.SetParent(transform);
            modelRoot.transform.localPosition = Vector3.zero;
            modelRoot.transform.localRotation = Quaternion.identity;

            // Scanner Head / Housing
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "Head";
            head.transform.SetParent(modelRoot.transform);
            head.transform.localPosition = new Vector3(0f, 0.08f, 0.04f);
            head.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
            head.transform.localScale = new Vector3(0.08f, 0.08f, 0.16f);
            SetColor(head, new Color(0.18f, 0.18f, 0.22f)); // Dark gunmetal
            DestroyCollider(head);

            // Scanner Pistol Grip / Handle
            GameObject grip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            grip.name = "Grip";
            grip.transform.SetParent(modelRoot.transform);
            grip.transform.localPosition = new Vector3(0f, -0.04f, -0.02f);
            grip.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
            grip.transform.localScale = new Vector3(0.05f, 0.15f, 0.06f);
            SetColor(grip, new Color(0.12f, 0.12f, 0.14f)); // Matt black rubber grip
            DestroyCollider(grip);

            // Trigger Button
            GameObject trigger = GameObject.CreatePrimitive(PrimitiveType.Cube);
            trigger.name = "Trigger";
            trigger.transform.SetParent(modelRoot.transform);
            trigger.transform.localPosition = new Vector3(0f, 0.02f, 0.02f);
            trigger.transform.localScale = new Vector3(0.025f, 0.035f, 0.03f);
            SetColor(trigger, new Color(0.85f, 0.2f, 0.2f)); // Red trigger button
            DestroyCollider(trigger);

            // Front Emitter Window (Glass)
            GameObject window = GameObject.CreatePrimitive(PrimitiveType.Cube);
            window.name = "LaserWindow";
            window.transform.SetParent(modelRoot.transform);
            window.transform.localPosition = new Vector3(0f, 0.10f, 0.125f);
            window.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
            window.transform.localScale = new Vector3(0.065f, 0.05f, 0.015f);
            SetColor(window, new Color(0.8f, 0.1f, 0.1f)); // Red glass window
            DestroyCollider(window);

            // Top Status LED Indicator
            GameObject led = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            led.name = "StatusLED";
            led.transform.SetParent(modelRoot.transform);
            led.transform.localPosition = new Vector3(0f, 0.13f, 0.0f);
            led.transform.localScale = new Vector3(0.025f, 0.025f, 0.025f);
            SetColor(led, Color.green);
            DestroyCollider(led);

            // LineRenderer for the scanning red laser beam
            _laserLine = gameObject.AddComponent<LineRenderer>();
            _laserLine.startWidth = 0.008f;
            _laserLine.endWidth = 0.008f;
            _laserLine.positionCount = 2;
            _laserLine.enabled = false;
            _laserLine.useWorldSpace = true;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            Material laserMat = new Material(shader) { color = Color.red };
            _laserLine.material = laserMat;
            _laserLine.startColor = Color.red;
            _laserLine.endColor = new Color(1f, 0.3f, 0.3f, 0.8f);

            // Flash light
            GameObject lightObj = new GameObject("ScanFlashLight");
            lightObj.transform.SetParent(transform);
            lightObj.transform.localPosition = new Vector3(0f, 0.10f, 0.14f);
            _scanFlashLight = lightObj.AddComponent<Light>();
            _scanFlashLight.type = LightType.Spot;
            _scanFlashLight.spotAngle = 45f;
            _scanFlashLight.range = 3.0f;
            _scanFlashLight.color = Color.red;
            _scanFlashLight.intensity = 2.5f;
            _scanFlashLight.enabled = false;

            // Adjust main collider
            BoxCollider boxCol = GetComponent<BoxCollider>();
            if (boxCol == null)
            {
                boxCol = gameObject.AddComponent<BoxCollider>();
            }
            boxCol.center = new Vector3(0f, 0.02f, 0.02f);
            boxCol.size = new Vector3(0.12f, 0.24f, 0.24f);
            _collider = boxCol;
        }

        private static void SetColor(GameObject go, Color color)
        {
            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                Material mat = new Material(shader) { color = color };
                r.material = mat;
            }
        }

        private static void DestroyCollider(GameObject go)
        {
            Collider c = go.GetComponent<Collider>();
            if (c != null)
            {
                DestroyImmediate(c);
            }
        }

        /// <summary>
        /// Attaches scanner to the player's hand hold point in first person.
        /// </summary>
        public void PickUp(Transform parent)
        {
            EnsureComponents();
            _isCarried = true;

            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
                _rigidbody.isKinematic = true;
            }
            if (_collider != null)
            {
                _collider.enabled = false;
            }

            transform.SetParent(parent);
            // Positioned nicely in bottom-right first-person view
            transform.localPosition = new Vector3(0.22f, -0.20f, 0.45f);
            transform.localRotation = Quaternion.Euler(10f, -5f, 0f);
        }

        /// <summary>
        /// Places the scanner firmly onto a surface.
        /// </summary>
        public void PlaceAt(Vector3 position, Quaternion rotation)
        {
            EnsureComponents();
            _isCarried = false;
            transform.SetParent(null);
            transform.position = position;
            transform.rotation = rotation;

            if (_rigidbody != null)
            {
                if (!_rigidbody.isKinematic)
                {
                    _rigidbody.linearVelocity = Vector3.zero;
                    _rigidbody.angularVelocity = Vector3.zero;
                }
                _rigidbody.isKinematic = true;
            }
            if (_collider != null)
            {
                _collider.enabled = true;
            }
        }

        /// <summary>
        /// Docks the scanner back into its cradle stand on the counter.
        /// </summary>
        public void PlaceInCradle()
        {
            if (_cradleTransform != null)
            {
                PlaceAt(_cradleTransform.position, _cradleTransform.rotation);
                transform.SetParent(_cradleTransform);
            }
            else
            {
                // Fallback position on desk
                PlaceAt(new Vector3(0.8f, 1.05f, 0f), Quaternion.Euler(0f, 180f, 0f));
            }
        }

        /// <summary>
        /// Performs a scan raycast, emitting a red laser line, sound effect, and triggers customer or box intake.
        /// </summary>
        public bool FireScan(Ray ray, LayerMask mask, out string feedback)
        {
            EnsureComponents();
            PlayBeep();

            Vector3 origin = transform.position + (transform.forward * 0.12f);
            Vector3 endPoint = origin + (ray.direction * _scanRange);

            bool hitFound = Physics.Raycast(ray, out RaycastHit hit, _scanRange, mask);
            if (hitFound)
            {
                endPoint = hit.point;
            }

            // Visual laser line & flash
            if (_laserCoroutine != null)
            {
                StopCoroutine(_laserCoroutine);
            }
            _laserCoroutine = StartCoroutine(LaserFlashRoutine(origin, endPoint));

            if (!hitFound)
            {
                feedback = "❌ Сканер: Луч не попал в штрихкод. Наведите сканер точнее на клиента или посылку.";
                return false;
            }

            // 1. Check Customer
            CustomerAI customer = hit.collider.GetComponentInParent<CustomerAI>();
            if (customer != null)
            {
                if (!customer.IsAtCounter)
                {
                    feedback = "⚠ Клиент еще не подошел к стойке!";
                    return false;
                }

                if (customer.IsScanned)
                {
                    string remIds = string.Join(", #", customer.RemainingOrderIds);
                    feedback = $"Штрихкод клиента уже отсканирован! Ожидаются товары: #{remIds}.";
                    return false;
                }

                customer.ScanOrder();
                if (WarehouseManager.Instance != null)
                {
                    WarehouseManager.Instance.HighlightSlotsForOrders(customer.RemainingOrderIds, 15.0f);
                }

                string idsStr = string.Join(", #", customer.RequestedOrderIds);
                feedback = $"✔ [Пик!] Штрихкод заказа ({customer.TotalRequestedCount} тов.: #{idsStr}) принят! Ячейки на стеллажах подсвечены.";
                return true;
            }

            // 2. Check Supply Box (delivery crate from truck)
            SupplyBox supplyBox = hit.collider.GetComponentInParent<SupplyBox>();
            if (supplyBox != null)
            {
                supplyBox.Unpack(out string unpackFb);
                feedback = $"✔ [Пик!] {unpackFb}";
                return true;
            }

            // 3. Check Individual Product Parcel (BoxItem)
            BoxItem box = hit.collider.GetComponentInParent<BoxItem>();
            if (box != null)
            {
                if (!box.IsIntakeScanned)
                {
                    box.SetIntakeScanned(true);
                    feedback = $"✔ [Пик!] Товар #{box.OrderId} успешно принят на склад! Теперь его можно положить на стеллаж.";
                    return true;
                }
                else
                {
                    feedback = $"Товар #{box.OrderId} уже принят (готов к размещению на стеллаж).";
                    return false;
                }
            }

            feedback = "❌ Сканер: Этот объект не содержит штрихкода (наведите на телефон клиента или коробку).";
            return false;
        }

        private IEnumerator LaserFlashRoutine(Vector3 start, Vector3 end)
        {
            if (_laserLine != null)
            {
                _laserLine.SetPosition(0, start);
                _laserLine.SetPosition(1, end);
                _laserLine.enabled = true;
            }
            if (_scanFlashLight != null)
            {
                _scanFlashLight.enabled = true;
            }

            yield return new WaitForSeconds(0.18f);

            if (_laserLine != null)
            {
                _laserLine.enabled = false;
            }
            if (_scanFlashLight != null)
            {
                _scanFlashLight.enabled = false;
            }
        }

        /// <summary>
        /// Plays barcode scanner confirmation beep.
        /// </summary>
        public void PlayBeep()
        {
            if (_audioSource != null && _beepClip != null)
            {
                _audioSource.PlayOneShot(_beepClip, 0.75f);
            }
        }

        /// <summary>
        /// Generates a clean synthetic PCM barcode scanner beep waveform.
        /// </summary>
        private static AudioClip CreateBeepClip(float frequency, float duration)
        {
            int sampleRate = 44100;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = (i < sampleCount * 0.85f) ? 1.0f : (1.0f - (i - sampleCount * 0.85f) / (sampleCount * 0.15f));
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * 0.45f;
            }

            AudioClip clip = AudioClip.Create("ScannerBeepClip", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
