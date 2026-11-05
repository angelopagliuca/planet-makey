using System;
using UnityEngine;
using UnityEngine.UI;

namespace PlanetMakey.Game.Round
{
    /// <summary>
    /// The night as a bar: a moon travels slowly from the left end to the right end and turns into the sun as
    /// it arrives, while the bar fills behind it from night purple to morning gold. It starts when dusk begins,
    /// follows the night, stays at the sun through dawn and fades out when the round is over. Put this on an
    /// empty UI object under a Canvas and place that object where the middle of the bar should be; the parts are
    /// made at run time. It only reads from <see cref="RoundController"/> and holds no game state.
    /// </summary>
    public sealed class NightBarView : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private RoundController _round;

        [Tooltip("Optional. The moon. When empty, a crescent is drawn.")]
        [SerializeField] private Sprite _moonIcon;

        [Tooltip("Optional. The sun. When empty, a sun with rays is drawn.")]
        [SerializeField] private Sprite _sunIcon;

        [Header("Bar")]
        [Tooltip("The length of the bar, in canvas pixels.")]
        [Min(10f)]
        [SerializeField] private float _barWidth = 560f;

        [Tooltip("The thickness of the bar, in canvas pixels.")]
        [Min(1f)]
        [SerializeField] private float _barHeight = 14f;

        [Tooltip("The color of the empty bar.")]
        [SerializeField] private Color _trackColor = new Color(0.12f, 0.10f, 0.22f, 0.75f);

        [Tooltip("The color of the filled part at the start of the night.")]
        [SerializeField] private Color _fillNightColor = new Color(0.45f, 0.35f, 0.85f, 1f);

        [Tooltip("The color of the filled part when the sun comes up.")]
        [SerializeField] private Color _fillDayColor = new Color(1f, 0.78f, 0.30f, 1f);

        [Header("Moon and sun")]
        [Tooltip("The size of the moon and of the sun it turns into, in canvas pixels.")]
        [Min(1f)]
        [SerializeField] private float _markerSize = 44f;

        [Tooltip("The size of the small sun past the right end of the bar, in canvas pixels.")]
        [Min(1f)]
        [SerializeField] private float _endSunSize = 30f;

        [Tooltip("How far along the bar (0 to 1) the moon starts to turn into the sun.")]
        [Range(0f, 1f)]
        [SerializeField] private float _sunBlendStart = 0.85f;

        [Header("Motion")]
        [Tooltip("Seconds the moon takes to catch up when the night jumps ahead, for example when the night is skipped. It is the time to cross the whole bar.")]
        [Min(0f)]
        [SerializeField] private float _catchUpSeconds = 1.5f;

        [Tooltip("Seconds the whole bar takes to fade in and out.")]
        [Min(0f)]
        [SerializeField] private float _showSeconds = 0.4f;

        [Header("Debug")]
        [Tooltip("Write to the Console when the bar is built. Turn it off when it is no longer needed.")]
        [SerializeField] private bool _debugLog = true;

        [NonSerialized] private RectTransform _rect;
        [NonSerialized] private CanvasGroup _group;
        [NonSerialized] private bool _addedGroup;
        [NonSerialized] private float _groupAlphaWas;
        [NonSerialized] private Sprite _generatedMoon;
        [NonSerialized] private Sprite _generatedSun;
        [NonSerialized] private Sprite _generatedCapsule;
        [NonSerialized] private bool _captured;

        // The parts of the bar.
        [NonSerialized] private Image _track;
        [NonSerialized] private Image _fill;
        [NonSerialized] private Image _endSun;
        [NonSerialized] private Image _moon;
        [NonSerialized] private Image _sun;

        // How far along the bar the moon is drawn, and how visible the bar is.
        [NonSerialized] private float _displayed;
        [NonSerialized] private float _visibleLevel;
        [NonSerialized] private bool _wasVisible;

        private void OnEnable()
        {
            if (_round == null)
            {
                Debug.LogError("NightBarView needs a RoundController, so it will do nothing.", this);
                return;
            }

            _rect = transform as RectTransform;
            if (_rect == null)
            {
                Debug.LogError("NightBarView must be on a UI object (one with a RectTransform) under a Canvas, so it will do nothing.", this);
                return;
            }

            if (GetComponentInParent<Canvas>() == null)
            {
                Debug.LogWarning("NightBarView is not under a Canvas, so nothing will be drawn.", this);
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

            BuildBar();

            _displayed = 0f;
            _visibleLevel = 0f;
            _wasVisible = false;
            _captured = true;

            if (_debugLog)
            {
                Debug.Log("[NightBarView] Built a bar " + _barWidth.ToString("0") + " pixels long.", this);
            }
        }

        private void OnDisable()
        {
            if (!_captured)
            {
                return;
            }

            _captured = false;
            DestroyPart(ref _track);
            DestroyPart(ref _fill);
            DestroyPart(ref _endSun);
            DestroyPart(ref _moon);
            DestroyPart(ref _sun);
            HudSprites.DestroySprite(_generatedMoon);
            HudSprites.DestroySprite(_generatedSun);
            HudSprites.DestroySprite(_generatedCapsule);
            _generatedMoon = null;
            _generatedSun = null;
            _generatedCapsule = null;

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
            RoundState state = _round.State;

            bool visible = RoundHudMath.IsRoundVisible(state);
            bool snap = visible && !_wasVisible;
            _wasVisible = visible;
            _visibleLevel = RoundHudMath.Approach(_visibleLevel, visible ? 1f : 0f, deltaTime, _showSeconds);
            _group.alpha = Mathf.SmoothStep(0f, 1f, _visibleLevel);

            // While the bar fades out after the round, it keeps the picture of how it ended.
            if (!visible)
            {
                return;
            }

            float progress = RoundHudMath.NightBarProgress(state, _round.StateProgress);
            _displayed = snap ? progress : RoundHudMath.Approach(_displayed, progress, deltaTime, _catchUpSeconds);

            float blend = RoundHudMath.SunBlend(_displayed, _sunBlendStart);
            float half = _barWidth * 0.5f;

            // The filled part grows from the left end, and the moon rides its tip.
            _fill.rectTransform.sizeDelta = new Vector2(Mathf.Max(0.01f, _displayed * _barWidth), _barHeight);
            _fill.color = Color.Lerp(_fillNightColor, _fillDayColor, Mathf.SmoothStep(0f, 1f, _displayed));

            Vector2 markerPosition = new Vector2(-half + _displayed * _barWidth, 0f);
            _moon.rectTransform.anchoredPosition = markerPosition;
            _sun.rectTransform.anchoredPosition = markerPosition;
            _moon.color = new Color(1f, 1f, 1f, 1f - blend);
            _sun.color = new Color(1f, 1f, 1f, blend);

            // The small sun at the end of the bar brightens as the moon nears it.
            _endSun.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.35f, 1f, blend));
        }

        // Makes the parts of the bar, around the middle of this object.
        private void BuildBar()
        {
            Sprite moonSprite = _moonIcon;
            if (moonSprite == null)
            {
                _generatedMoon = HudSprites.CreateMoon();
                moonSprite = _generatedMoon;
            }

            Sprite sunSprite = _sunIcon;
            if (sunSprite == null)
            {
                _generatedSun = HudSprites.CreateSun();
                sunSprite = _generatedSun;
            }

            Vector2 middle = new Vector2(0.5f, 0.5f);
            float half = _barWidth * 0.5f;

            // The track and the fill have round ends: a sprite cut into three parts, with the ends scaled to
            // be as wide as the bar is tall.
            Canvas canvas = GetComponentInParent<Canvas>();
            float referencePixelsPerUnit = canvas != null ? canvas.referencePixelsPerUnit : 100f;
            float capsuleMultiplier = HudSprites.CapsuleScaleMultiplier(_barHeight, referencePixelsPerUnit);
            _generatedCapsule = HudSprites.CreateCapsule();

            _track = HudSprites.CreateImage(_rect, "Night bar track", _generatedCapsule, new Vector2(_barWidth, _barHeight), middle, middle);
            MakeRound(_track, capsuleMultiplier);
            _track.color = _trackColor;

            _fill = HudSprites.CreateImage(_rect, "Night bar fill", _generatedCapsule, new Vector2(0.01f, _barHeight), middle, new Vector2(0f, 0.5f));
            MakeRound(_fill, capsuleMultiplier);
            _fill.rectTransform.anchoredPosition = new Vector2(-half, 0f);
            _fill.color = _fillNightColor;

            Vector2 endSunSize = new Vector2(_endSunSize, _endSunSize);
            _endSun = HudSprites.CreateImage(_rect, "Night bar end sun", sunSprite, endSunSize, middle, middle);
            _endSun.rectTransform.anchoredPosition = new Vector2(half + _endSunSize * 0.9f, 0f);

            Vector2 markerSize = new Vector2(_markerSize, _markerSize);
            _moon = HudSprites.CreateImage(_rect, "Night bar moon", moonSprite, markerSize, middle, middle);
            _sun = HudSprites.CreateImage(_rect, "Night bar sun", sunSprite, markerSize, middle, middle);
            _moon.rectTransform.anchoredPosition = new Vector2(-half, 0f);
            _sun.rectTransform.anchoredPosition = new Vector2(-half, 0f);
            _sun.color = new Color(1f, 1f, 1f, 0f);
        }

        private static void MakeRound(Image bar, float capsuleMultiplier)
        {
            bar.type = Image.Type.Sliced;
            bar.pixelsPerUnitMultiplier = capsuleMultiplier;
            bar.preserveAspect = false;
        }

        private static void DestroyPart(ref Image part)
        {
            if (part != null)
            {
                Destroy(part.gameObject);
            }

            part = null;
        }
    }
}
