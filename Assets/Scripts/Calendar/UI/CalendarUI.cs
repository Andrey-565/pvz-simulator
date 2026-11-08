using System;
using UnityEngine;
using PVZ.Delivery;
using PVZ.Player;
using PVZ.Utils;

namespace PVZ.Calendar.UI
{
    /// <summary>
    /// Interactive graphical UI for the wall calendar.
    /// Displays months, days of the week, crossed-out past days, red active day,
    /// shift delivery status & cooldowns, and the "Завершить смену" action button.
    /// </summary>
    public class CalendarUI : MonoBehaviour
    {
        private bool _isOpen;
        private PlayerController _activePlayer;

        private readonly string[] _daysOfWeek = new string[] { "ПН", "ВТ", "СР", "ЧТ", "ПТ", "СБ", "ВС" };

        /// <summary>
        /// Indicates whether the calendar window is currently displayed.
        /// </summary>
        public bool IsOpen => _isOpen;

        private void Update()
        {
            if (_isOpen)
            {
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E))
                {
                    CloseCalendar();
                }
            }
        }

        /// <summary>
        /// Opens the calendar interface and freezes player movement.
        /// </summary>
        public void OpenCalendar(PlayerController player)
        {
            if (_isOpen)
            {
                return;
            }

            _activePlayer = player;
            _isOpen = true;

            if (_activePlayer != null)
            {
                _activePlayer.SetMovementLocked(true);
                _activePlayer.SetCursorLocked(false);
            }
        }

        /// <summary>
        /// Closes the calendar and restores player controls.
        /// </summary>
        public void CloseCalendar()
        {
            if (!_isOpen)
            {
                return;
            }

            _isOpen = false;

            if (_activePlayer != null)
            {
                _activePlayer.SetMovementLocked(false);
                _activePlayer.SetCursorLocked(true);
                _activePlayer = null;
            }
        }

        private void OnGUI()
        {
            if (!_isOpen)
            {
                return;
            }

            var cm = CalendarManager.Instance;
            if (cm == null)
            {
                return;
            }

            int currentDay = cm.CurrentDay;
            string month = cm.MonthName;
            int year = cm.Year;
            int daysInMonth = cm.DaysInMonth;
            int firstDayOffset = cm.FirstDayOfWeekOffset; // 0 = Mon, 6 = Sun

            float windowWidth = 720f;
            float windowHeight = 620f;
            float startX = (Screen.width - windowWidth) * 0.5f;
            float startY = (Screen.height - windowHeight) * 0.5f;

            // Semi-transparent darkened background overlay
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none);

            // Main Calendar Window Box
            GUI.Box(new Rect(startX, startY, windowWidth, windowHeight), GUIContent.none);

            GUILayout.BeginArea(new Rect(startX + 20, startY + 16, windowWidth - 40, windowHeight - 32));

            // Header Title
            GUIStyle headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            headerStyle.normal.textColor = Color.white;
            GUILayout.Label("📅 НАСТЕННЫЙ КАЛЕНДАРЬ РАБОЧИХ СМЕН", headerStyle);

            // Month & Year Banner
            GUIStyle monthStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            monthStyle.normal.textColor = Color.yellow;
            GUILayout.Box($"<b>{month.ToUpper()} {year}</b>   •   Текущая смена: День {currentDay}", monthStyle, GUILayout.Height(32));

            GUILayout.Space(8);

            // Days of the week row
            GUILayout.BeginHorizontal();
            for (int i = 0; i < 7; i++)
            {
                bool isWeekend = (i == 5 || i == 6);
                GUIStyle dowStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                dowStyle.normal.textColor = isWeekend ? new Color(1f, 0.45f, 0.45f) : Color.cyan;
                GUILayout.Box(_daysOfWeek[i], dowStyle, GUILayout.Width(92), GUILayout.Height(26));
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            // Days Grid (6 rows x 7 cols)
            int dayNumber = 1;
            Color originalBgColor = GUI.backgroundColor;

            for (int row = 0; row < 6; row++)
            {
                if (dayNumber > daysInMonth)
                {
                    break;
                }

                GUILayout.BeginHorizontal();
                for (int col = 0; col < 7; col++)
                {
                    int cellIndex = row * 7 + col;
                    if (cellIndex < firstDayOffset || dayNumber > daysInMonth)
                    {
                        // Blank cell outside month days
                        GUI.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.4f);
                        GUILayout.Box(string.Empty, GUILayout.Width(92), GUILayout.Height(46));
                        GUI.backgroundColor = originalBgColor;
                    }
                    else
                    {
                        int day = dayNumber;
                        if (day < currentDay)
                        {
                            // Past day: CROSSED OUT WITH AN X!
                            GUI.backgroundColor = new Color(0.25f, 0.25f, 0.28f, 0.9f);
                            GUIStyle pastStyle = new GUIStyle(GUI.skin.box)
                            {
                                fontSize = 12,
                                alignment = TextAnchor.MiddleCenter
                            };
                            pastStyle.normal.textColor = new Color(0.65f, 0.65f, 0.65f);

                            string label = $"<s>{day}</s>\n<color=#FF3333><b>✖</b></color>";
                            GUILayout.Box(label, pastStyle, GUILayout.Width(92), GUILayout.Height(46));
                            GUI.backgroundColor = originalBgColor;
                        }
                        else if (day == currentDay)
                        {
                            // Active day: COLORED IN BOLD RED!
                            GUI.backgroundColor = new Color(0.92f, 0.15f, 0.15f, 1.0f);
                            GUIStyle activeStyle = new GUIStyle(GUI.skin.box)
                            {
                                fontSize = 13,
                                fontStyle = FontStyle.Bold,
                                alignment = TextAnchor.MiddleCenter
                            };
                            activeStyle.normal.textColor = Color.white;

                            string label = $"<b><size=15>{day}</size></b>\n<size=10><b>★ СМЕНА</b></size>";
                            GUILayout.Box(label, activeStyle, GUILayout.Width(92), GUILayout.Height(46));
                            GUI.backgroundColor = originalBgColor;
                        }
                        else
                        {
                            // Future day: normal styling
                            GUI.backgroundColor = new Color(0.35f, 0.35f, 0.40f, 0.7f);
                            GUIStyle futureStyle = new GUIStyle(GUI.skin.box)
                            {
                                fontSize = 13,
                                fontStyle = FontStyle.Bold,
                                alignment = TextAnchor.MiddleCenter
                            };
                            futureStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);

                            GUILayout.Box($"{day}", futureStyle, GUILayout.Width(92), GUILayout.Height(46));
                            GUI.backgroundColor = originalBgColor;
                        }

                        dayNumber++;
                    }
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(2);
            }

            GUILayout.Space(10);

            // Shift Deliveries Summary Box
            var dm = DeliveryManager.Instance;
            GUILayout.BeginVertical("box");
            if (dm != null)
            {
                int remainingDeliveries = dm.DeliveriesRemainingToday;
                int maxDeliveries = dm.MaxDeliveriesPerShift;

                string deliveryStatus = (remainingDeliveries > 0)
                    ? $"<color=#00FF00>Доступно: {remainingDeliveries} из {maxDeliveries}</color> (Поставки бесплатны)"
                    : $"<color=red>Исчерпаны на смену (0 из {maxDeliveries})</color>";

                string cdStatus = dm.IsCooldownActive
                    ? $"<color=yellow>Идёт КД: {TimeFormatter.FormatMinutesSeconds(dm.RemainingCooldown)}</color>"
                    : "<color=#00FF00>Готов к заказу (КД нет)</color>";

                GUILayout.Label($"<b>Смена #{cm.TotalShiftsCompleted + 1} (День {currentDay}):</b>   Поставки: <b>{deliveryStatus}</b>");
                GUILayout.Label($"<b>КД между поставками:</b> {cdStatus} | Базовый кулдаун: <b>{TimeFormatter.FormatMinutesSeconds(dm.DeliveryCooldownDuration)}</b>");
            }
            GUILayout.EndVertical();

            GUILayout.Space(12);

            // Action Buttons Row
            GUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.2f, 0.7f, 0.3f);
            if (GUILayout.Button("🌙 ЗАВЕРШИТЬ СМЕНУ", GUILayout.Height(44)))
            {
                cm.CompleteShift();
            }
            GUI.backgroundColor = originalBgColor;

            GUILayout.Space(14);

            if (GUILayout.Button("ЗАКРЫТЬ [E / Esc]", GUILayout.Width(180), GUILayout.Height(44)))
            {
                CloseCalendar();
            }

            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }
    }
}
