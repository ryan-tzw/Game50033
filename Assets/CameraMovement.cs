using UnityEngine;
using System;
using System.Collections;

namespace Game50033
{
    public class CameraMovement : MonoBehaviour
    {
        [Tooltip("Camera position relative to what it's looking at. Matches a camera at (0, 10, -10) looking at (0, 0, 0).")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 10f, -10f);

        [Tooltip("How tightly it follows the player. Higher = tighter, 0 = snaps.")]
        [SerializeField, Min(0f)] private float smoothing = 8f;

        [Header("Focus (e.g. boss intro)")]
        [Tooltip("How fast it pans to and from a focus target. Lower = slower, more cinematic pan.")]
        [SerializeField, Min(0.1f)] private float panSmoothing = 2.5f;
        [Tooltip("Roughly how long the pan takes before the hold time starts.")]

        public event Action FocusFinished;   // e.g. to start the boss fight when the intro ends
        public bool IsFocusing => _focusTarget != null;

        private Transform _target;
        private Transform _focusTarget;
        private Coroutine _focusRoutine;
        private GameSession _session;

        private void Awake()
        {
            _session = FindFirstObjectByType<GameSession>();
        }

        private void OnEnable()
        {
            if (_session == null) return;
            _session.PlayerRegistered += HandlePlayerRegistered;
            _session.PlayerUnregistered += HandlePlayerUnregistered;

            if (_session.Players.Count > 0) SetTarget(_session.Players[0].transform, snap: true);
        }

        private void OnDisable()
        {
            if (_session == null) return;
            _session.PlayerRegistered -= HandlePlayerRegistered;
            _session.PlayerUnregistered -= HandlePlayerUnregistered;
        }

        private void HandlePlayerRegistered(Player player)
        {
            if (_target == null) SetTarget(player.transform, snap: true);
        }

        private void HandlePlayerUnregistered(Player player)
        {
            if (_target == player.transform)
            {
                _target = null;
                if (_session.Players.Count > 0) SetTarget(_session.Players[0].transform, snap: true);
            }
        }

        public void SetTarget(Transform newTarget, bool snap = false)
        {
            _target = newTarget;
            if (snap && _target != null && !IsFocusing) transform.position = _target.position + offset;
        }

        // Pans to 'focus', stays on it for 'holdTime' seconds, then pans back to the player.
        public void FocusOn(Transform focus, float panTime, float holdTime)
        {
            if (focus == null) return;
            if (_focusRoutine != null) StopCoroutine(_focusRoutine);
            _focusRoutine = StartCoroutine(FocusRoutine(focus, panTime, holdTime));
        }

        private IEnumerator FocusRoutine(Transform focus, float panTime, float holdTime)
        {
            _focusTarget = focus;
            yield return new WaitForSeconds(panTime + holdTime);   // pan there, then hold

            _focusTarget = null;                                    // pan back to the player
            yield return new WaitForSeconds(panTime);

            _focusRoutine = null;
            FocusFinished?.Invoke();
        }

        private void LateUpdate()
        {
            // look at the focus target if there is one, otherwise the player
            Transform follow = _focusTarget != null ? _focusTarget : _target;
            if (follow == null) return;

            // pan slowly while focusing or returning from a focus, follow tightly otherwise
            float speed = (_focusTarget != null || _focusRoutine != null) ? panSmoothing : smoothing;

            Vector3 desired = follow.position + offset;
            if (speed <= 0f)
                transform.position = desired;
            else
                transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-speed * Time.deltaTime));
        }
    }
}
