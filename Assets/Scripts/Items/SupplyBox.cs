using System;
using System.Collections.Generic;
using UnityEngine;

namespace PVZ.Items
{
    /// <summary>
    /// Represents a large sealed delivery shipping crate arriving on the delivery truck.
    /// Can be carried into the PVZ facility and unpacked to reveal 5 individual products.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(BoxCollider))]
    public class SupplyBox : MonoBehaviour
    {
        [Header("Supply Box Properties")]
        [Tooltip("The unique product/order IDs contained inside this shipping box (always 5).")]
        [SerializeField] private List<int> _productIds = new List<int>();

        [SerializeField] private bool _isUnpacked = false;
        [SerializeField] private bool _isCarried = false;

        private Rigidbody _rigidbody;
        private BoxCollider _collider;
        private static AudioClip _unpackAudioClip;

        /// <summary>
        /// Gets the list of product IDs inside this delivery box.
        /// </summary>
        public IReadOnlyList<int> ProductIds => _productIds;

        /// <summary>
        /// True if the box has already been unpacked.
        /// </summary>
        public bool IsUnpacked => _isUnpacked;

        /// <summary>
        /// True if currently held in player hands.
        /// </summary>
        public bool IsCarried => _isCarried;

        private void Awake()
        {
            EnsureComponents();
            BuildVisualsIfNeeded();

            if (_unpackAudioClip == null)
            {
                _unpackAudioClip = CreateUnpackAudioClip();
            }
        }

        private void EnsureComponents()
        {
            if (_rigidbody == null)
            {
                _rigidbody = GetComponent<Rigidbody>();
            }
            if (_collider == null)
            {
                _collider = GetComponent<BoxCollider>();
            }

            if (_rigidbody != null)
            {
                _rigidbody.mass = 3.5f; // Heavy shipping crate feel
                _rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }

            if (_collider != null)
            {
                _collider.size = new Vector3(0.65f, 0.52f, 0.55f);
                _collider.center = Vector3.zero;
            }
        }

        /// <summary>
        /// Initializes the crate with 5 product IDs and sets up shipping label.
        /// </summary>
        public void Initialize(List<int> productIds)
        {
            _productIds = (productIds != null) ? new List<int>(productIds) : new List<int>();
            _isUnpacked = false;
            BuildVisualsIfNeeded();
        }

        private void BuildVisualsIfNeeded()
        {
            if (transform.Find("SupplyBoxVisuals") != null)
            {
                return;
            }

            GameObject visuals = new GameObject("SupplyBoxVisuals");
            visuals.transform.SetParent(transform);
            visuals.transform.localPosition = Vector3.zero;
            visuals.transform.localRotation = Quaternion.identity;

            // Main cardboard crate body
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "CrateBody";
            body.transform.SetParent(visuals.transform);
            body.transform.localPosition = Vector3.zero;
            body.transform.localScale = new Vector3(0.65f, 0.52f, 0.55f);
            DestroyImmediate(body.GetComponent<Collider>());
            SetColor(body, new Color(0.64f, 0.46f, 0.28f)); // Kraft corrugated cardboard

            // Packing tape strip across the top seam
            GameObject tape = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tape.name = "PackingTape";
            tape.transform.SetParent(visuals.transform);
            tape.transform.localPosition = new Vector3(0f, 0.262f, 0f);
            tape.transform.localScale = new Vector3(0.12f, 0.008f, 0.555f);
            DestroyImmediate(tape.GetComponent<Collider>());
            SetColor(tape, new Color(0.78f, 0.60f, 0.38f)); // Scotch tape

            // Delivery Manifest Sticker on front side
            GameObject sticker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sticker.name = "DeliveryLabel";
            sticker.transform.SetParent(visuals.transform);
            sticker.transform.localPosition = new Vector3(0f, 0.05f, -0.278f);
            sticker.transform.localScale = new Vector3(0.34f, 0.24f, 0.008f);
            DestroyImmediate(sticker.GetComponent<Collider>());
            SetColor(sticker, new Color(0.96f, 0.96f, 0.94f)); // White label
        }

        private static void SetColor(GameObject go, Color color)
        {
            Renderer rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                rend.material = new Material(shader) { color = color };
            }
        }

        /// <summary>
        /// Attaches the supply crate to the player's hold point.
        /// </summary>
        public void PickUp(Transform holdPoint)
        {
            EnsureComponents();
            _isCarried = true;

            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = true;
            }
            if (_collider != null)
            {
                _collider.enabled = false;
            }

            transform.SetParent(holdPoint);
            transform.localPosition = new Vector3(0f, -0.15f, 0.20f);
            transform.localRotation = Quaternion.identity;
        }

        /// <summary>
        /// Places the supply crate firmly onto a surface.
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
        /// Unpacks this supply box, spawning 5 individual product parcels neatly on the surface.
        /// </summary>
        public List<BoxItem> Unpack(out string feedback)
        {
            if (_isUnpacked)
            {
                feedback = "Коробка уже распакована!";
                return new List<BoxItem>();
            }

            _isUnpacked = true;

            // Play cardboard opening sound at position
            if (_unpackAudioClip != null)
            {
                AudioSource.PlayClipAtPoint(_unpackAudioClip, transform.position, 1.0f);
            }

            Vector3 basePos = transform.position;
            Quaternion baseRot = transform.rotation;

            // Offsets for 5 products layout
            Vector3[] productOffsets = new Vector3[]
            {
                new Vector3(-0.24f, 0.12f, -0.15f),
                new Vector3( 0.00f, 0.12f, -0.15f),
                new Vector3( 0.24f, 0.12f, -0.15f),
                new Vector3(-0.14f, 0.12f,  0.15f),
                new Vector3( 0.14f, 0.12f,  0.15f)
            };

            var spawnedProducts = new List<BoxItem>();

            for (int i = 0; i < 5; i++)
            {
                int orderId = (i < _productIds.Count) ? _productIds[i] : (100 + i);
                Vector3 spawnPos = basePos + (baseRot * productOffsets[i]);

                BoxItem product = CreateProductParcel(orderId, spawnPos, baseRot);
                spawnedProducts.Add(product);
            }

            string idsStr = string.Join(", #", _productIds);
            feedback = $"📦 Распаковано 5 товаров (#{idsStr})! Отсканируйте их сканером и разложите по ячейкам стеллажей.";

            // Destroy the empty shipping box
            Destroy(gameObject);

            return spawnedProducts;
        }

        /// <summary>
        /// Instantiates an individual product parcel with compact dimensions and barcode sticker.
        /// </summary>
        public static BoxItem CreateProductParcel(int orderId, Vector3 worldPos, Quaternion rotation)
        {
            GameObject parcelObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            parcelObj.name = $"Product_Parcel_{orderId}";
            parcelObj.transform.position = worldPos;
            parcelObj.transform.rotation = rotation;
            parcelObj.transform.localScale = new Vector3(0.32f, 0.22f, 0.26f);

            var rb = parcelObj.AddComponent<Rigidbody>();
            rb.mass = 1.0f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            // Visual variety: kraft parcel, white courier polybag, or delivery box
            Renderer rend = parcelObj.GetComponent<Renderer>();
            if (rend != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");

                int styleIndex = Mathf.Abs(orderId) % 3;
                Color parcelColor = styleIndex switch
                {
                    0 => new Color(0.78f, 0.62f, 0.44f), // Kraft cardboard box
                    1 => new Color(0.92f, 0.93f, 0.96f), // White courier polybag
                    _ => new Color(0.84f, 0.70f, 0.54f)  // Retail postal box
                };
                rend.material = new Material(shader) { color = parcelColor };
            }

            BoxItem boxItem = parcelObj.AddComponent<BoxItem>();
            boxItem.Initialize(orderId, isIntakeScanned: false);
            return boxItem;
        }

        /// <summary>
        /// Generates procedural tape tear / cardboard unpacking sound.
        /// </summary>
        private static AudioClip CreateUnpackAudioClip()
        {
            int sampleRate = 44100;
            float duration = 0.28f;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            var rand = new System.Random(42);

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                float envelope = Mathf.Sin(t * Mathf.PI);

                // High frequency tape noise + low frequency cardboard thump
                float noise = (float)(rand.NextDouble() * 2.0 - 1.0) * 0.4f;
                float cardboardThump = Mathf.Sin(2f * Mathf.PI * 180f * (float)i / sampleRate) * 0.6f;

                samples[i] = (noise + cardboardThump) * envelope * 0.7f;
            }

            AudioClip clip = AudioClip.Create("Cardboard_Unpack_PCM", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
