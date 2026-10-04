using System;
using System.Globalization;
using PlanetMakey.Core;
using UnityEngine;

namespace PlanetMakey.Hits
{
    /// <summary>
    /// Debug overlay that draws the most recent hits where they landed, in their ball color.
    /// It shows raw sensor positions, so it is also the way to see what the hardware reports
    /// before calibration.
    /// </summary>
    /// <remarks>
    /// Drawn with OnGUI, which allocates every frame. That is acceptable for a debug tool:
    /// disable this component when it is not needed.
    /// </remarks>
    public sealed class HitDebugOverlay : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("Every hit source whose hits should be drawn. If one of them is the mouse source, the color it will fire next is shown too.")]
        [SerializeField] private HitSource[] _sources;

        [Tooltip("Display colors for the ball colors. Markers are white if this is empty.")]
        [SerializeField] private BallColorPalette _palette;

        [Header("Look")]
        [Tooltip("How many recent hits are kept on screen.")]
        [SerializeField] private int _maxMarkers = 8;

        [Tooltip("Marker edge length in pixels.")]
        [SerializeField] private float _markerSize = 24f;

        [Tooltip("Seconds a marker takes to fade out.")]
        [SerializeField] private float _markerLifetime = 3f;

        [Tooltip("Text size in pixels. Large enough to read on the projector.")]
        [SerializeField] private int _fontSize = 18;

        // Fixed-size ring buffer of recent hits, so a hit never allocates a collection.
        private BallHit[] _hits;
        private float[] _hitTimes;
        private string[] _hitLabels;
        private int _nextIndex;
        private int _hitCount;

        // The status line is rebuilt only when the selected color changes.
        private string _statusText;
        private BallColor _statusColor;

        // Not kept across a script recompile in Play mode, so it is rebuilt from the skin.
        [NonSerialized] private GUIStyle _labelStyle;

        // The mouse source found in _sources, or null. It is looked up from that list and not
        // wired separately, so the status line can never show for a source whose hits are not drawn.
        [NonSerialized] private MouseHitSource _mouseSource;

        private void OnEnable()
        {
            EnsureBuffers();
            _mouseSource = null;

            if (_sources == null || _sources.Length == 0)
            {
                Debug.LogWarning(
                    "HitDebugOverlay has no hit sources, so it will not draw any hits. " +
                    "Add the hit sources to its Sources list.",
                    this);
                return;
            }

            for (int i = 0; i < _sources.Length; i++)
            {
                HitSource source = _sources[i];
                if (source == null)
                {
                    continue;
                }

                source.HitReceived += OnHitReceived;

                MouseHitSource mouseSource = source as MouseHitSource;
                if (mouseSource != null)
                {
                    _mouseSource = mouseSource;
                }
            }
        }

        private void OnDisable()
        {
            if (_sources == null)
            {
                return;
            }

            for (int i = 0; i < _sources.Length; i++)
            {
                if (_sources[i] != null)
                {
                    _sources[i].HitReceived -= OnHitReceived;
                }
            }
        }

        private void EnsureBuffers()
        {
            if (_hits != null)
            {
                return;
            }

            // Done here and not in Awake: after a script recompile in Play mode Unity calls
            // OnEnable again but not Awake, and it does not bring the hit buffer back.
            int capacity = Mathf.Max(1, _maxMarkers);
            _hits = new BallHit[capacity];
            _hitTimes = new float[capacity];
            _hitLabels = new string[capacity];
            _nextIndex = 0;
            _hitCount = 0;
        }

        private void OnHitReceived(BallHit hit)
        {
            _hits[_nextIndex] = hit;

            // Unscaled, so markers still fade if the game is paused with a zero time scale.
            _hitTimes[_nextIndex] = Time.unscaledTime;

            // Built once per hit and not per frame. Invariant culture keeps the decimal point
            // the same as in the hardware protocol, whatever the PC's language is.
            _hitLabels[_nextIndex] = string.Format(
                CultureInfo.InvariantCulture,
                "{0}  {1:0.00}, {2:0.00}",
                hit.Color,
                hit.Position.x,
                hit.Position.y);

            _nextIndex = (_nextIndex + 1) % _hits.Length;
            _hitCount = Mathf.Min(_hitCount + 1, _hits.Length);
        }

        private void OnGUI()
        {
            // OnGUI also runs for layout and input events. Draw once per frame only.
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            EnsureLabelStyle();
            DrawMarkers();
            DrawStatus();
            GUI.color = Color.white;
        }

        private void EnsureLabelStyle()
        {
            if (_labelStyle == null)
            {
                // GUI.skin is only available inside OnGUI, so the style cannot be made earlier.
                _labelStyle = new GUIStyle(GUI.skin.label);
                _labelStyle.normal.textColor = Color.white;
            }

            // Set every time so a change in the Inspector shows while playing.
            _labelStyle.fontSize = _fontSize;
        }

        private void DrawMarkers()
        {
            float now = Time.unscaledTime;

            for (int i = 0; i < _hitCount; i++)
            {
                float age = now - _hitTimes[i];
                if (age >= _markerLifetime)
                {
                    continue;
                }

                BallHit hit = _hits[i];

                // Sensor space has its origin at the bottom left; OnGUI at the top left.
                float x = hit.Position.x * Screen.width;
                float y = (1f - hit.Position.y) * Screen.height;

                Color color = GetDisplayColor(hit.Color);
                color.a = 1f - (age / _markerLifetime);
                GUI.color = color;

                float half = _markerSize * 0.5f;
                Rect markerRect = new Rect(x - half, y - half, _markerSize, _markerSize);
                GUI.DrawTexture(markerRect, Texture2D.whiteTexture);

                Rect labelRect = new Rect(x + half + 4f, y - half, 300f, _fontSize + 8f);
                GUI.Label(labelRect, _hitLabels[i], _labelStyle);
            }
        }

        private void DrawStatus()
        {
            if (_mouseSource == null)
            {
                return;
            }

            BallColor selected = _mouseSource.SelectedColor;
            if (string.IsNullOrEmpty(_statusText) || selected != _statusColor)
            {
                _statusColor = selected;
                _statusText = "Mouse color: " + selected + "  (keys 1-4)";
            }

            float swatchSize = _fontSize;
            GUI.color = GetDisplayColor(selected);
            GUI.DrawTexture(new Rect(10f, 10f, swatchSize, swatchSize), Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUI.Label(new Rect(18f + swatchSize, 6f, 600f, _fontSize + 8f), _statusText, _labelStyle);
        }

        private Color GetDisplayColor(BallColor ballColor)
        {
            if (_palette == null)
            {
                return Color.white;
            }

            return _palette.GetColor(ballColor);
        }
    }
}
