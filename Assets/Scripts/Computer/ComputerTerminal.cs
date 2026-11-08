using UnityEngine;
using PVZ.Player;
using PVZ.Computer.UI;

namespace PVZ.Computer
{
    /// <summary>
    /// Physical 3D computer terminal object on the desk.
    /// Handles player interaction, camera focus, cursor release, and UI toggling.
    /// </summary>
    public class ComputerTerminal : MonoBehaviour
    {
        [Header("Components")]
        [Tooltip("The ComputerUI script component managing the desktop window.")]
        [SerializeField] private ComputerUI _computerUI;

        [Tooltip("Camera focus point transform near the monitor screen.")]
        [SerializeField] private Transform _screenFocusPoint;

        private PlayerController _activePlayer;
        private bool _isOpen;

        /// <summary>
        /// Indicates whether the computer is currently being operated by the player.
        /// </summary>
        public bool IsOpen => _isOpen;

        private void Awake()
        {
            if (_computerUI == null)
            {
                _computerUI = GetComponent<ComputerUI>();
                if (_computerUI == null)
                {
                    _computerUI = gameObject.AddComponent<ComputerUI>();
                }
            }
        }

        private void Update()
        {
            if (_isOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                CloseTerminal();
            }
        }

        /// <summary>
        /// Activates terminal mode: freezes player movement and unlocks cursor for desktop UI interaction.
        /// </summary>
        /// <param name="player">The player character opening the computer.</param>
        public void OpenTerminal(PlayerController player)
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

            if (_computerUI != null)
            {
                _computerUI.SetOpen(true);
            }
        }

        /// <summary>
        /// Closes terminal mode and returns control to player.
        /// </summary>
        public void CloseTerminal()
        {
            if (!_isOpen)
            {
                return;
            }

            _isOpen = false;

            if (_computerUI != null)
            {
                _computerUI.SetOpen(false);
            }

            if (_activePlayer != null)
            {
                _activePlayer.SetMovementLocked(false);
                _activePlayer.SetCursorLocked(true);
                _activePlayer = null;
            }
        }
    }
}
