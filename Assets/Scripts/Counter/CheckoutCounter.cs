using System;
using UnityEngine;
using PVZ.Items;
using PVZ.Warehouse;
using PVZ.Customers;
using PVZ.Economy;

namespace PVZ.Counter
{
    /// <summary>
    /// Interactive service desk component managing the Open/Closed shop sign,
    /// order barcode scanner, and customer parcel handovers.
    /// </summary>
    public class CheckoutCounter : MonoBehaviour
    {
        [Header("Physical Elements")]
        [Tooltip("Transform of the Open/Closed sign tablet on the counter.")]
        [SerializeField] private Transform _statusSignTablet;

        [Tooltip("Transform of the barcode scanner device or stand.")]
        [SerializeField] private Transform _barcodeScanner;

        [Tooltip("Interactive physical handheld barcode scanner.")]
        [SerializeField] private BarcodeScannerItem _scannerItem;

        [Tooltip("Renderer of the status sign tablet to toggle between green and red.")]
        [SerializeField] private Renderer _statusSignRenderer;

        [Header("Runtime State")]
        [SerializeField] private string _scannerResult = string.Empty;
        private float _scannerResultTimer;

        /// <summary>
        /// Gets the barcode scanner transform.
        /// </summary>
        public Transform BarcodeScanner => _barcodeScanner;

        /// <summary>
        /// Gets the interactive handheld scanner item.
        /// </summary>
        public BarcodeScannerItem ScannerItem => _scannerItem;

        /// <summary>
        /// Gets the Open/Closed sign tablet transform.
        /// </summary>
        public Transform StatusSignTablet => _statusSignTablet;

        private void Start()
        {
            UpdateSignVisuals();
            EnsureScannerInScene();
            EnsureComputerOnCounter();
            if (CustomerQueueManager.Instance != null)
            {
                CustomerQueueManager.Instance.OnStoreOpenStatusChanged += HandleStoreStatusChanged;
            }
        }

        /// <summary>
        /// Ensures the computer terminal is positioned right on the service counter desk next to the scanner.
        /// </summary>
        public void EnsureComputerOnCounter()
        {
            Computer.ComputerTerminal terminal = FindFirstObjectByType<Computer.ComputerTerminal>();
            if (terminal == null)
            {
                return;
            }

            Transform targetRoot = (terminal.transform.parent != null && terminal.transform.parent.name == "ComputerWorkstation")
                ? terminal.transform.parent
                : terminal.transform;

            if (Vector3.Distance(transform.position, terminal.transform.position) > 2.2f)
            {
                terminal.transform.SetParent(transform);
                terminal.transform.localPosition = new Vector3(-0.25f, 1.05f, 0.05f);
                terminal.transform.localRotation = Quaternion.identity;

                if (targetRoot != terminal.transform)
                {
                    Destroy(targetRoot.gameObject);
                }
            }
        }

        /// <summary>
        /// Ensures an interactive BarcodeScannerItem exists on the counter.
        /// </summary>
        public void EnsureScannerInScene()
        {
            if (_scannerItem != null)
            {
                return;
            }

            _scannerItem = FindFirstObjectByType<BarcodeScannerItem>();
            if (_scannerItem != null)
            {
                return;
            }

            // Create scanner item on the counter
            GameObject scannerGo = new GameObject("Handheld_Barcode_Scanner");
            Vector3 spawnPos = (_barcodeScanner != null)
                ? _barcodeScanner.position + Vector3.up * 0.15f
                : transform.position + new Vector3(0.8f, 1.05f, 0f);
            scannerGo.transform.position = spawnPos;
            scannerGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var rb = scannerGo.AddComponent<Rigidbody>();
            rb.mass = 0.5f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            var box = scannerGo.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.02f, 0.02f);
            box.size = new Vector3(0.12f, 0.24f, 0.24f);

            _scannerItem = scannerGo.AddComponent<BarcodeScannerItem>();
            if (_scannerItem != null && _barcodeScanner != null)
            {
                _scannerItem.CradleTransform = _barcodeScanner;
            }
        }

        private void OnDestroy()
        {
            if (CustomerQueueManager.Instance != null)
            {
                CustomerQueueManager.Instance.OnStoreOpenStatusChanged -= HandleStoreStatusChanged;
            }
        }

        private void Update()
        {
            if (_scannerResultTimer > 0f)
            {
                _scannerResultTimer -= Time.deltaTime;
                if (_scannerResultTimer <= 0f)
                {
                    _scannerResult = string.Empty;
                }
            }
        }

        private void HandleStoreStatusChanged(bool isOpen)
        {
            UpdateSignVisuals();
        }

        /// <summary>
        /// Toggles store open/closed state.
        /// </summary>
        public bool ToggleOpenClosed(out string feedback)
        {
            if (CustomerQueueManager.Instance == null)
            {
                feedback = "CustomerQueueManager не найден в сцене!";
                return false;
            }

            bool result = CustomerQueueManager.Instance.ToggleOpen(out feedback);
            UpdateSignVisuals();
            return result;
        }

        /// <summary>
        /// Scans the front customer's order barcode: unveils order ID, starts the timer, and highlights shelf slot.
        /// </summary>
        public void ScanCustomerOrder()
        {
            if (CustomerQueueManager.Instance == null || CustomerQueueManager.Instance.FirstInQueue == null)
            {
                SetScannerFeedback("🔍 Сканер: Очередь пуста! Клиент еще не подошел.");
                return;
            }

            CustomerAI customer = CustomerQueueManager.Instance.FirstInQueue;

            if (!customer.IsAtCounter)
            {
                SetScannerFeedback("⚠ Сканер: Клиент еще подходит к стойке! Подождите, пока он встанет на место.");
                return;
            }

            // Trigger scan on the customer to unveil order number and start speed timer
            customer.ScanOrder();

            if (WarehouseManager.Instance != null)
            {
                WarehouseManager.Instance.HighlightSlotsForOrders(customer.RemainingOrderIds, 15.0f);
            }

            string neededList = string.Join(", #", customer.RemainingOrderIds);
            SetScannerFeedback($"✔ [Пик!] Клиент отсканирован! Требуется {customer.TotalRequestedCount} тов. (#{neededList}). Ячейки подсвечены! Таймер пошел.");
        }

        /// <summary>
        /// Attempts to hand over the given box to the first customer in line.
        /// </summary>
        public bool TryServeCustomer(BoxItem box, out string feedback)
        {
            if (box == null)
            {
                feedback = "Вы не держите посылку!";
                return false;
            }

            if (CustomerQueueManager.Instance == null || CustomerQueueManager.Instance.FirstInQueue == null)
            {
                feedback = "На стойке нет клиентов! Подождите посетителя.";
                return false;
            }

            CustomerAI customer = CustomerQueueManager.Instance.FirstInQueue;
            bool success = customer.TryReceiveOrder(box, out int ratingDelta, out float payment, out feedback);
            return success;
        }

        private void SetScannerFeedback(string msg)
        {
            _scannerResult = msg;
            _scannerResultTimer = 5.0f;
        }

        private void UpdateSignVisuals()
        {
            if (_statusSignRenderer == null && _statusSignTablet != null)
            {
                _statusSignRenderer = _statusSignTablet.GetComponent<Renderer>();
            }

            if (_statusSignRenderer != null)
            {
                bool isOpen = CustomerQueueManager.Instance != null && CustomerQueueManager.Instance.IsOpen;
                _statusSignRenderer.material.color = isOpen ? new Color(0.1f, 0.8f, 0.2f) : new Color(0.85f, 0.15f, 0.15f);
            }
        }

        private void OnGUI()
        {
            if (!string.IsNullOrEmpty(_scannerResult) && _scannerResultTimer > 0f)
            {
                GUIStyle scanStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                scanStyle.normal.textColor = Color.yellow;
                GUI.Box(new Rect((Screen.width - 650) * 0.5f, 105, 650, 34), _scannerResult, scanStyle);
            }
        }
    }
}
