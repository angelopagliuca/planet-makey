using System;
using UnityEngine;
using UnityEngine.UI;

namespace PlanetMakey.Game.Round
{
    /// <summary>
    /// The cows still safe, shown as a row of small icons: lit while the cows are safe, faded once Makey has
    /// taken them. It shows one icon per cow in the herd; with N cows safe the first N are lit, so the icons at
    /// the end fade as cows are lost, and the one that just faded pops. The row is shown from dusk to the end
    /// of dawn, so at dawn the lit icons are the score, and fades out when the round is over. Put this on an
    /// empty UI object under a Canvas and place that object where the middle of the row should be (for example
    /// anchored at the bottom center); the icons are made at run time, centered on it. It only reads from <see cref="RoundController"/> and holds no game state.
    /// </summary>
    public sealed class CowIconsView : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private RoundController _round;

        [Tooltip("Optional. The cow icon. When empty, a simple cow face is drawn. The icon is tinted by the colors below, so a white or light picture works best.")]
        [SerializeField] private Sprite _cowIcon;

        [Header("Look")]
        [Tooltip("The width and height of one icon, in canvas pixels.")]
        [Min(1f)]
        [SerializeField] private float _iconSize = 40f;

        [Tooltip("The gap between icons, in canvas pixels.")]
        [Min(0f)]
        [SerializeField] private float _spacing = 8f;

        [Tooltip("The color of an icon for a cow that is safe.")]
        [SerializeField] private Color _litColor = Color.white;

        [Tooltip("The color of an icon for a cow that was taken. Its alpha is how faint it is.")]
        [SerializeField] private Color _takenColor = new Color(0.3f, 0.3f, 0.36f, 0.35f);

        [Header("Motion")]
        [Tooltip("Seconds an icon takes to fade when its cow is taken.")]
        [Min(0f)]
        [SerializeField] private float _fadeSeconds = 0.3f;

        [Tooltip("How much bigger the icon of a cow just taken swells, as a multiple. 1 for no pop.")]
        [Min(1f)]
        [SerializeField] private float _popScale = 1.35f;

        [Tooltip("Seconds the pop lasts.")]
        [Min(0.01f)]
        [SerializeField] private float _popSeconds = 0.3f;

        [Tooltip("Seconds the whole row takes to fade in and out.")]
        [Min(0f)]
        [SerializeField] private float _showSeconds = 0.4f;

        [Header("Debug")]
        [Tooltip("Write to the Console when the icons are built. Turn it off when it is no longer needed.")]
        [SerializeField] private bool _debugLog = true;

        [NonSerialized] private RectTransform _rect;
        [NonSerialized] private CanvasGroup _group;
        [NonSerialized] private bool _addedGroup;
        [NonSerialized] private float _groupAlphaWas;
        [NonSerialized] private Sprite _generated;
        [NonSerialized] private bool _captured;

        // The icons and how each one is doing, by place in the row.
        [NonSerialized] private Image[] _icons;
        [NonSerialized] private float[] _levels;
        [NonSerialized] private bool[] _wasLit;
        [NonSerialized] private float[] _popLeft;
        [NonSerialized] private int _builtTotal;
        [NonSerialized] private float _visibleLevel;
        [NonSerialized] private bool _wasVisible;

        private void OnEnable()
        {
            if (_round == null)
            {
                Debug.LogError("CowIconsView needs a RoundController, so it will do nothing.", this);
                return;
            }

            _rect = transform as RectTransform;
            if (_rect == null)
            {
                Debug.LogError("CowIconsView must be on a UI object (one with a RectTransform) under a Canvas, so it will do nothing.", this);
                return;
            }

            if (GetComponentInParent<Canvas>() == null)
            {
                Debug.LogWarning("CowIconsView is not under a Canvas, so nothing will be drawn.", this);
            }

            _group = GetComponent<CanvasGroup>();
            _addedGroup = _group == null;
            if (_addedGroup)
            {
                _group = gameObject.AddComponent<CanvasGroup>();
            }

            _groupAlphaWas = _group.alpha;
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            _generated = _cowIcon == null ? HudSprites.CreateCow() : null;
            _icons = new Image[0];
            _builtTotal = -1;
            _visibleLevel = 0f;
            _wasVisible = false;
            _captured = true;
        }

        private void OnDisable()
        {
            if (!_captured)
            {
                return;
            }

            _captured = false;
            DestroyIcons();
            HudSprites.DestroySprite(_generated);
            _generated = null;

            // The group may already be destroyed when the scene shuts down.
            if (_group != null)
            {
                if (_addedGroup)
                {
                    Destroy(_group);
                }
                else
                {
                    _group.alpha = _groupAlphaWas;
                }
            }

            _group = null;
        }

        private void Update()
        {
            if (!_captured)
            {
                return;
            }

            float deltaTime = Time.deltaTime;

            int total = _round.CowsTotal;
            if (total != _builtTotal)
            {
                BuildIcons(total);
            }

            bool visible = RoundHudMath.IsRoundVisible(_round.State);
            bool snap = visible && !_wasVisible;
            _wasVisible = visible;
            _visibleLevel = RoundHudMath.Approach(_visibleLevel, visible ? 1f : 0f, deltaTime, _showSeconds);
            _group.alpha = Mathf.SmoothStep(0f, 1f, _visibleLevel);

            // While the row fades out after the round, it keeps the picture of how it ended.
            if (!visible)
            {
                return;
            }

            int safe = _round.CowsSafe;
            for (int i = 0; i < _icons.Length; i++)
            {
                UpdateIcon(i, RoundHudMath.IsCowLit(i, safe), snap, deltaTime);
            }
        }

        private void UpdateIcon(int index, bool lit, bool snap, float deltaTime)
        {
            if (snap)
            {
                // The row has just appeared for a new round: show it as it is, with no fades.
                _levels[index] = lit ? 1f : 0f;
                _wasLit[index] = lit;
                _popLeft[index] = 0f;
            }
            else
            {
                if (_wasLit[index] && !lit)
                {
                    _popLeft[index] = _popSeconds;
                }

                _wasLit[index] = lit;
                _levels[index] = RoundHudMath.Approach(_levels[index], lit ? 1f : 0f, deltaTime, _fadeSeconds);
            }

            Image icon = _icons[index];
            icon.color = Color.Lerp(_takenColor, _litColor, Mathf.SmoothStep(0f, 1f, _levels[index]));

            float scale = 1f;
            if (_popLeft[index] > 0f)
            {
                _popLeft[index] = Mathf.Max(0f, _popLeft[index] - deltaTime);
                float through = 1f - _popLeft[index] / _popSeconds;
                scale = 1f + (_popScale - 1f) * Mathf.Sin(Mathf.PI * through);
            }

            icon.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        // Makes the row of icons, centered on this object.
        private void BuildIcons(int total)
        {
            DestroyIcons();

            Sprite sprite = _cowIcon != null ? _cowIcon : _generated;
            int count = Mathf.Max(0, total);
            _icons = new Image[count];
            _levels = new float[count];
            _wasLit = new bool[count];
            _popLeft = new float[count];

            Vector2 size = new Vector2(_iconSize, _iconSize);
            Vector2 middle = new Vector2(0.5f, 0.5f);
            float firstX = -(count - 1) * 0.5f * (_iconSize + _spacing);
            for (int i = 0; i < count; i++)
            {
                Image icon = HudSprites.CreateImage(_rect, "Cow icon " + (i + 1), sprite, size, middle, middle);
                icon.rectTransform.anchoredPosition = new Vector2(firstX + i * (_iconSize + _spacing), 0f);
                icon.color = _takenColor;
                _icons[i] = icon;
            }

            _builtTotal = total;

            if (_debugLog)
            {
                Debug.Log("[CowIconsView] Built " + count + " icons.", this);
            }
        }

        private void DestroyIcons()
        {
            if (_icons == null)
            {
                return;
            }

            for (int i = 0; i < _icons.Length; i++)
            {
                if (_icons[i] != null)
                {
                    Destroy(_icons[i].gameObject);
                }
            }

            _icons = new Image[0];
        }
    }
}
