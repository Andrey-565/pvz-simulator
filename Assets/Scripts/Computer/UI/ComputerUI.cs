using System;
using System.Collections.Generic;
using UnityEngine;
using PVZ.Economy;
using PVZ.Warehouse;
using PVZ.Delivery;
using PVZ.Utils;

namespace PVZ.Computer.UI
{
    /// <summary>
    /// Interactive desktop computer UI for ordering parcel shipments, viewing delivery status and timer,
    /// and inspecting warehouse inventory slots.
    /// </summary>
    public class ComputerUI : MonoBehaviour
    {
        private bool _isOpen;
        private int _selectedTab = 0; // 0 = Market, 1 = Warehouse
        private Vector2 _scrollPos;
        private string _statusMessage = string.Empty;
        private float _statusTimer = 0f;

        /// <summary>
        /// Fires when a supply shipment of boxes is ordered.
        /// Param: number of boxes ordered.
        /// </summary>
        public static event Action<int> OnSupplyBatchOrdered;

        /// <summary>
        /// True if the computer terminal window is currently active on screen.
        /// </summary>
        public bool IsOpen => _isOpen;

        /// <summary>
        /// Opens or closes the computer interface.
        /// </summary>
        public void SetOpen(bool open)
        {
            _isOpen = open;
            if (_isOpen)
            {
                _statusMessage = string.Empty;
            }
        }

        private void Update()
        {
            if (_statusTimer > 0f)
            {
                _statusTimer -= Time.deltaTime;
                if (_statusTimer <= 0f)
                {
                    _statusMessage = string.Empty;
                }
            }
        }

        private void OnGUI()
        {
            if (!_isOpen)
            {
                return;
            }

            int width = 740;
            int height = 540;
            int x = (Screen.width - width) / 2;
            int y = (Screen.height - height) / 2;

            Rect windowRect = new Rect(x, y, width, height);
            GUI.Box(windowRect, GUIContent.none);
            GUI.Window(999, windowRect, DrawWindowContent, "ПВЗ-ОС v1.0 — Рабочий терминал сотрудника");
        }

        private void DrawWindowContent(int windowId)
        {
            int balance = EconomyManager.Instance != null ? EconomyManager.Instance.CurrentBalance : 0;

            // Top Bar
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>Сотрудник:</b> Оператор ПВЗ | <b>Баланс кассы:</b> <color=green>{balance:N0} ₽</color>", GUILayout.Height(25));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("✕ Закрыть [Esc]", GUILayout.Width(110), GUILayout.Height(25)))
            {
                SendMessageUpwards("CloseTerminal", SendMessageOptions.DontRequireReceiver);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // Tab Selection
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(_selectedTab == 0, " 📦 Заказ товаров (Market) ", "Button", GUILayout.Height(30)))
            {
                _selectedTab = 0;
            }
            if (GUILayout.Toggle(_selectedTab == 1, " 📋 Складской учёт (Inventory) ", "Button", GUILayout.Height(30)))
            {
                _selectedTab = 1;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(12);

            // Content per tab
            if (_selectedTab == 0)
            {
                DrawMarketTab(balance);
            }
            else
            {
                DrawWarehouseTab();
            }

            // Status message
            if (!string.IsNullOrEmpty(_statusMessage))
            {
                GUILayout.FlexibleSpace();
                GUIStyle msgStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 14
                };
                msgStyle.normal.textColor = Color.yellow;
                GUILayout.Label(_statusMessage, msgStyle);
                GUILayout.Space(5);
            }
        }

        private void DrawMarketTab(int balance)
        {
            // Delivery Status Card inside PC UI
            if (DeliveryManager.Instance != null)
            {
                var dm = DeliveryManager.Instance;
                GUILayout.BeginVertical("box");

                if (dm.IsDeliveryPending)
                {
                    string arrivalStr = TimeFormatter.FormatMinutesSeconds(dm.RemainingCountdown);
                    GUILayout.Label($"<b>СТАТУС ПОСТАВКИ:</b> <color=cyan>🚛 В ПУТИ...</color> | Прибытие через: <b>{arrivalStr}</b>", GUILayout.Height(22));
                    GUILayout.Label($"<size=12>Фура с {dm.PendingBatchCount} коробками прибудет к входу на улице.</size>");
                }
                else if (dm.Truck != null && dm.Truck.IsArrived)
                {
                    GUILayout.Label($"<b>СТАТУС ПОСТАВКИ:</b> <color=#00FF00>📦 НА РАЗГРУЗКЕ!</color> Фура ожидает разгрузки у входа на улице.", GUILayout.Height(22));
                    GUILayout.Label($"<size=12>Осталось коробок в кузове: <b>{dm.Truck.RemainingBoxCount} шт.</b> Выгрузите их на склад.</size>");
                }
                else if (dm.IsDeliveryActive)
                {
                    GUILayout.Label($"<b>СТАТУС ПОСТАВКИ:</b> <color=yellow>🚛 ФУРА НА УЛИЦЕ</color> | Маневрирование / завершение разгрузки.", GUILayout.Height(22));
                    GUILayout.Label("<size=12>Ожидайте освобождения въезда перед оформлением новой поставки.</size>");
                }
                else if (dm.DeliveriesRemainingToday <= 0)
                {
                    GUILayout.Label($"<b>СТАТУС ПОСТАВКИ:</b> <color=red>⛔ ЛИМИТ НА СМЕНУ ИСЧЕРПАН ({dm.DeliveriesOrderedToday} из {dm.MaxDeliveriesPerShift})</color>", GUILayout.Height(22));
                    GUILayout.Label("<size=12>Завершите рабочую смену на настенном календаре склада, чтобы начать новый рабочий день.</size>");
                }
                else if (dm.IsCooldownActive)
                {
                    string cdStr = TimeFormatter.FormatMinutesSeconds(dm.RemainingCooldown);
                    GUILayout.Label($"<b>СТАТУС ПОСТАВКИ:</b> <color=yellow>⏳ КУЛДАУН ПОСТАВЩИКА</color> | До следующего заказа: <b>{cdStr}</b>", GUILayout.Height(22));
                    GUILayout.Label("<size=12>Базовый интервал между поставками: 1 мин. 40 сек. Сбрасывается при завершении смены на календаре.</size>");
                }
                else
                {
                    GUILayout.Label($"<b>СТАТУС ПОСТАВКИ:</b> <color=#00FF00>✔ Доступно к заказу</color> (Осталось на смену: <b>{dm.DeliveriesRemainingToday} из {dm.MaxDeliveriesPerShift}</b>)", GUILayout.Height(22));
                    GUILayout.Label("<size=12>Поставки бесплатны! Кулдаун между заказами: 1 мин. 40 сек. (Лимит: 2 поставки за смену).</size>");
                }

                GUILayout.EndVertical();
                GUILayout.Space(10);
            }

            GUILayout.Label("<b>Бесплатные поставки партий товаров (Лимит: 2 в рабочий день):</b>");
            GUILayout.Space(6);

            DrawSupplyCard("Малая партия коробок", 3, "3 коробки — быстрая разгрузка на полки");
            GUILayout.Space(6);
            DrawSupplyCard("Стандартная партия коробок", 6, "6 коробок — оптимальный запас на смену");
            GUILayout.Space(6);
            DrawSupplyCard("Оптовая партия коробок", 10, "10 коробок — максимальный объём за одну поставку");
        }

        private void DrawSupplyCard(string title, int boxCount, string description)
        {
            GUILayout.BeginVertical("box");
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical();
            GUILayout.Label($"<b>{title}</b> ({boxCount} шт.) — <color=#00FF00>Бесплатно (0 ₽)</color>");
            GUILayout.Label($"<size=11>{description}</size>");
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            var dm = DeliveryManager.Instance;
            bool canOrder = dm != null && dm.CanOrderDelivery;
            string btnText = "Заказать (0 ₽)";

            if (dm != null)
            {
                if (dm.DeliveriesRemainingToday <= 0)
                {
                    btnText = "Лимит (2/2)";
                }
                else if (dm.IsDeliveryActive)
                {
                    btnText = "Поставка в пути";
                }
                else if (dm.IsCooldownActive)
                {
                    btnText = $"КД {TimeFormatter.FormatMinutesSeconds(dm.RemainingCooldown)}";
                }
            }

            GUI.enabled = canOrder;
            if (GUILayout.Button(btnText, GUILayout.Width(150), GUILayout.Height(36)))
            {
                OrderBatch(boxCount);
            }
            GUI.enabled = true;

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void OrderBatch(int boxCount)
        {
            var dm = DeliveryManager.Instance;
            if (dm == null || !dm.CanOrderDelivery)
            {
                if (dm != null && dm.DeliveriesRemainingToday <= 0)
                {
                    _statusMessage = "❌ Лимит поставок на смену исчерпан (2/2)! Завершите смену на календаре.";
                }
                else if (dm != null && dm.IsCooldownActive)
                {
                    _statusMessage = $"⏳ Дождитесь окончания кулдауна ({TimeFormatter.FormatMinutesSeconds(dm.RemainingCooldown)})!";
                }
                else
                {
                    _statusMessage = "❌ Нельзя заказать новую поставку прямо сейчас!";
                }
                _statusTimer = 4f;
                return;
            }

            OnSupplyBatchOrdered?.Invoke(boxCount);
            _statusMessage = $"✔ Поставка из {boxCount} коробок оформлена бесплатно! Фура прибудет через {TimeFormatter.FormatMinutesSeconds(20f)}.";
            _statusTimer = 6f;
        }

        private void DrawWarehouseTab()
        {
            if (WarehouseManager.Instance == null)
            {
                GUILayout.Label("WarehouseManager не обнаружен в сцене.");
                return;
            }

            var allSlots = WarehouseManager.Instance.GetAllSlots();
            int occupiedCount = WarehouseManager.Instance.GetOccupiedSlots().Count;
            int totalSlots = allSlots.Count;

            GUILayout.Label($"<b>Занятость ячеек склада:</b> {occupiedCount} из {totalSlots} (Свободно: {totalSlots - occupiedCount})");
            GUILayout.Space(8);

            _scrollPos = GUILayout.BeginScrollView(_scrollPos, "box", GUILayout.Height(320));

            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>Ячейка</b>", GUILayout.Width(80));
            GUILayout.Label("<b>Статус</b>", GUILayout.Width(120));
            GUILayout.Label("<b>Номер заказа</b>", GUILayout.Width(140));
            GUILayout.Label("<b>Действие</b>");
            GUILayout.EndHorizontal();

            GUILayout.Box(GUIContent.none, GUILayout.Height(2));

            foreach (var slot in allSlots)
            {
                GUILayout.BeginHorizontal();

                GUILayout.Label($"<b>[{slot.SlotId}]</b>", GUILayout.Width(80));

                if (slot.IsEmpty)
                {
                    GUILayout.Label("<color=green>Свободно</color>", GUILayout.Width(120));
                    GUILayout.Label("—", GUILayout.Width(140));
                    GUILayout.Label("—");
                }
                else
                {
                    GUILayout.Label("<color=yellow>Занято</color>", GUILayout.Width(120));
                    GUILayout.Label($"Заказ #{slot.CurrentBox.OrderId}", GUILayout.Width(140));
                    GUILayout.Label("Готов к выдаче");
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
        }
    }
}
