using System;
using UnityEngine;

namespace PlanetMakey.Game.Ufo
{
    /// <summary>
    /// A soft shadow on the ground under Makey, and a glowing pool where the beam lands. The shadow
    /// ties him to the field and shows how high he hovers; the pool lights the cow he is after. It
    /// only reads from <see cref="Makey"/> and holds no game state.
    /// </summary>
    /// <remarks>
    /// Needs no art: it builds a soft round texture in code and draws two flat sprites that lie on the
    /// terrain, found with a ray straight down. The terrain (or whatever the ground is) must have a
    /// collider on a layer in Ground Layers. The sprites are created when this component is enabled
    /// and destroyed when it is disabled.
    /// </remarks>
    public sealed class MakeyGroundView : MonoBehaviour
    {
        // Width of the round texture in pixels. It is drawn two world units wide at scale 1.
        private const int TextureSize = 64;

        // How far above Makey the ray for the pool starts, so a cow standing on a hill is not missed.
        private const float RayStartHeight = 8f;

        // The longest ray, in world units.
        private const float RayLength = 200f;

        [Header("Wiring")]
        [SerializeField] private Makey _makey;

        [Tooltip("The layers the ground is on. The terrain needs a collider on one of them. In this project the terrain is on the Environment layer (11), which is the default. Leave out the Target layer, so the ray does not stop on Makey.")]
        [SerializeField] private LayerMask _groundLayers = 1 << 11;

        [Tooltip("How far above the ground the shadow and the pool are drawn, in world units. A little keeps them from flickering where they meet the ground.")]
        [Min(0.001f)]
        [SerializeField] private float _liftAboveGround = 0.05f;

        [Header("Shadow")]
        [Tooltip("The shadow's color. Its see-through is set below.")]
        [SerializeField] private Color _shadowColor = Color.black;

        [Tooltip("How dark the shadow is when Makey is low, from 0 to 1.")]
        [Range(0f, 1f)]
        [SerializeField] private float _shadowMaxAlpha = 0.4f;

        [Tooltip("The shadow's radius when Makey is on the ground, in world units.")]
        [Min(0.01f)]
        [SerializeField] private float _shadowRadius = 1.4f;

        [Tooltip("How much wider the shadow gets for each world unit Makey is above the ground.")]
        [Min(0f)]
        [SerializeField] private float _shadowGrowthPerUnit = 0.08f;

        [Tooltip("The height above the ground, in world units, at which the shadow has faded out completely.")]
        [Min(0.1f)]
        [SerializeField] private float _shadowFadeHeight = 14f;

        [Header("Beam pool")]
        [Tooltip("The color of the light where the beam lands, usually the beam's color.")]
        [SerializeField] private Color _poolColor = new Color(0.71f, 0.95f, 0.23f, 1f);

        [Tooltip("How bright the pool is at its strongest, from 0 to 1.")]
        [Range(0f, 1f)]
        [SerializeField] private float _poolMaxAlpha = 0.5f;

        [Tooltip("The pool's radius, in world units.")]
        [Min(0.01f)]
        [SerializeField] private float _poolRadius = 1.6f;

        [Tooltip("Pulses per second of the pool while the cow rises.")]
        [Min(0f)]
        [SerializeField] private float _poolPulsesPerSecond = 1.5f;

        [Tooltip("How much the pool's brightness pulses, as a share of its brightness.")]
        [Range(0f, 1f)]
        [SerializeField] private float _poolPulseDepth = 0.25f;

        [Header("Debug")]
        [Tooltip("Write the setup and the first time a sprite finds no ground to the Console. Turn it off when it is no longer needed.")]
        [SerializeField] private bool _debugLog = true;

        [NonSerialized] private Texture2D _texture;
        [NonSerialized] private Sprite _sprite;
        [NonSerialized] private SpriteRenderer _shadow;
        [NonSerialized] private SpriteRenderer _pool;
        [NonSerialized] private bool _warnedNoGround;

        // When the component is added in the Editor, the ground layer is looked up by name.
        private void Reset()
        {
            int environment = LayerMask.GetMask("Environment");
            if (environment != 0)
            {
                _groundLayers = environment;
            }
        }

        private void OnEnable()
        {
            if (_makey == null)
            {
                Debug.LogError("MakeyGroundView needs a Makey, so it will do nothing.", this);
                return;
            }

            if (_groundLayers.value == 0)
            {
                Debug.LogWarning("MakeyGroundView has no Ground Layers, so it will find no ground and show nothing.", this);
            }

            _texture = BuildSoftRoundTexture();
            _sprite = Sprite.Create(_texture, new Rect(0f, 0f, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), TextureSize * 0.5f);
            _shadow = CreateFlatSprite("Makey shadow (runtime)");
            _pool = CreateFlatSprite("Makey beam pool (runtime)");
            _warnedNoGround = false;

            if (_debugLog)
            {
                Debug.Log("[MakeyGroundView] Enabled. Ground layers mask " + _groundLayers.value + ". Shadow radius " + _shadowRadius.ToString("0.0#")
                    + ", beam pool radius " + _poolRadius.ToString("0.0#") + ".", this);
            }
        }

        private void OnDisable()
        {
            DestroyIfAlive(_shadow != null ? _shadow.gameObject : null);
            DestroyIfAlive(_pool != null ? _pool.gameObject : null);
            DestroyIfAlive(_sprite);
            DestroyIfAlive(_texture);
            _shadow = null;
            _pool = null;
            _sprite = null;
            _texture = null;
        }

        // After Makey's Update has moved him, so the shadow never trails a frame behind.
        private void LateUpdate()
        {
            if (_shadow == null || _pool == null)
            {
                return;
            }

            MakeyState state = _makey.State;
            UpdateShadow(state);
            UpdatePool(state);
        }

        private void UpdateShadow(MakeyState state)
        {
            if (state == MakeyState.Away)
            {
                _shadow.enabled = false;
                return;
            }

            Vector3 above = _makey.transform.position;
            if (!TryFindGround(above, out RaycastHit hit))
            {
                _shadow.enabled = false;
                return;
            }

            // He flies in and out above the ground, so the shadow fades with him.
            float presence = 1f;
            if (state == MakeyState.Entering)
            {
                presence = _makey.StateProgress;
            }
            else if (state == MakeyState.Leaving)
            {
                presence = 1f - _makey.StateProgress;
            }

            float height = Mathf.Max(0f, above.y - hit.point.y);
            float low = 1f - Mathf.Clamp01(height / _shadowFadeHeight);
            float radius = _shadowRadius * (1f + height * _shadowGrowthPerUnit);

            Place(_shadow, hit, radius, _shadowColor, _shadowMaxAlpha * low * presence);
        }

        private void UpdatePool(MakeyState state)
        {
            AbductionPhase phase = _makey.AbductionPhase;
            int index = _makey.TargetCowIndex;
            bool beamOn = state == MakeyState.Shielded
                && (phase == AbductionPhase.LoweringBeam || phase == AbductionPhase.Lifting)
                && index >= 0
                && _makey.CowHerd != null;

            if (!beamOn)
            {
                _pool.enabled = false;
                return;
            }

            Vector3 cow = _makey.CowHerd.RestPosition(index);
            if (!TryFindGround(new Vector3(cow.x, cow.y + RayStartHeight, cow.z), out RaycastHit hit))
            {
                _pool.enabled = false;
                return;
            }

            // The pool grows in as the beam reaches down, then pulses while the cow rises.
            float strength = phase == AbductionPhase.LoweringBeam ? _makey.AbductionProgress : 1f;
            float pulse = 1f - _poolPulseDepth * (0.5f + 0.5f * Mathf.Sin(Time.time * 2f * Mathf.PI * _poolPulsesPerSecond));
            Place(_pool, hit, _poolRadius * Mathf.Lerp(0.6f, 1f, strength), _poolColor, _poolMaxAlpha * strength * pulse);
        }

        private bool TryFindGround(Vector3 from, out RaycastHit hit)
        {
            if (Physics.Raycast(from, Vector3.down, out hit, RayLength, _groundLayers, QueryTriggerInteraction.Ignore))
            {
                return true;
            }

            if (_debugLog && !_warnedNoGround)
            {
                _warnedNoGround = true;
                Debug.LogWarning("[MakeyGroundView] No ground found below " + from.ToString("F1") + ". The terrain needs a collider on a layer in Ground Layers, or the shadow and the beam pool will not show.", this);
            }

            return false;
        }

        // Lays a sprite flat on the ground at the ray's hit, turned to the ground's slope.
        private void Place(SpriteRenderer target, RaycastHit hit, float radius, Color color, float alpha)
        {
            Transform t = target.transform;
            t.position = hit.point + hit.normal * _liftAboveGround;

            // A sprite shows from the side its forward points away from, so it looks along the ground's normal.
            t.rotation = Quaternion.LookRotation(-hit.normal, Vector3.forward);
            t.localScale = Vector3.one * radius;

            color.a = Mathf.Clamp01(alpha);
            target.color = color;
            target.enabled = alpha > 0.001f;
        }

        private SpriteRenderer CreateFlatSprite(string objectName)
        {
            GameObject created = new GameObject(objectName);
            created.hideFlags = HideFlags.DontSave;
            SpriteRenderer sprite = created.AddComponent<SpriteRenderer>();
            sprite.sprite = _sprite;
            sprite.enabled = false;
            return sprite;
        }

        // White in the middle and fading smoothly to nothing at the edge, so the sprite can be tinted
        // any color and has no hard rim.
        private static Texture2D BuildSoftRoundTexture()
        {
            Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.DontSave;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[TextureSize * TextureSize];
            float half = (TextureSize - 1) * 0.5f;
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float distance = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                    float edge = 1f - Mathf.Clamp01(distance);
                    float alpha = edge * edge * (3f - 2f * edge);
                    pixels[y * TextureSize + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static void DestroyIfAlive(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
