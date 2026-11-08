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

        [Tooltip("Transform of the barcode scanner device.")]
        [SerializeField] private Transform _barcodeScanner;

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
        /// Gets the Open/Closed sign tablet transform.
        /// </summary>
        public Transform StatusSignTablet => _statusSignTablet;

        private void Start()
        {
            UpdateSignVisuals();
            if (CustomerQueueManager.Instance != null)
            {
                CustomerQueueManager.Instance.OnStoreOpenStatusChanged += HandleStoreStatusChanged;
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

            // Trigger scan on the customer to unveil order number and start speed timer
            customer.ScanOrder();

            int orderId = customer.TargetOrderId;

            if (WarehouseManager.Instance != null)
            {
                WarehouseSlot slot = WarehouseManager.Instance.FindSlotByOrderId(orderId);
                if (slot != null)
                {
                    slot.Highlight(10.0f);
                    SetScannerFeedback($"✔ [Пик!] Заказ #{orderId} отсканирован! Лежит в ячейке [{slot.SlotId}] (подсвечена). Таймер пошел!");
                    return;
                }
            }

            SetScannerFeedback($"✔ [Пик!] Заказ #{orderId} отсканирован! В ячейках не найден (проверьте пол или фуру). Таймер пошел!");
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
            bool success = customer.TryReceiveOrder(box, out int ratingDelta, out int payment, out feedback);
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
