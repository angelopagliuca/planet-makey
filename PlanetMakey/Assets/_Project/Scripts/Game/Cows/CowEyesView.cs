using System;
using PlanetMakey.Game.Ufo;
using UnityEngine;
using UnityEngine.Rendering;

namespace PlanetMakey.Game.Cows
{
    /// <summary>
    /// Big googly eyes for the cow Makey is abducting. Every cow gets a pair of white balls with a dark pupil in
    /// front of each, made at run time and hidden until the cow is targeted: then they pop up bigger than the
    /// cow's own eyes, with pupils that shrink to terrified dots, wobble a little and always look at the camera,
    /// whichever way the cow has turned. They shrink away again if the cow is let go. Where the eyes sit is set in cow space on this component, because
    /// the model is one mesh with its eyes painted on; the offsets are read every frame, so they can be tuned in
    /// Play mode, and selecting this object draws where they go on every cow.
    /// </summary>
    public sealed class CowEyesView : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("The herd and the camera come from Makey, so it is wired only once.")]
        [SerializeField] private Makey _makey;

        [Tooltip("The material of the eyeballs: a plain white one.")]
        [SerializeField] private Material _eyeWhiteMaterial;

        [Tooltip("The material of the pupils: a plain black one.")]
        [SerializeField] private Material _pupilMaterial;

        [Header("Where the eyes are")]
        [Tooltip("The left eye's place on the cow, in meters from the cow's pivot at her normal size, along the cow object's own axes (x, y and z of the object, so turn on Pivot > Local in the Editor to see them; a model imported turned over has z up and its front along -y). Select this object to see the eyes drawn on every cow.")]
        [SerializeField] private Vector3 _leftEyeOffset = new Vector3(-0.1f, -0.4f, 0.45f);

        [Tooltip("The right eye's place on the cow, in meters, along the cow object's own axes.")]
        [SerializeField] private Vector3 _rightEyeOffset = new Vector3(0.1f, -0.4f, 0.45f);

        [Tooltip("The radius of a fully grown eye, in meters.")]
        [Min(0.001f)]
        [SerializeField] private float _eyeRadius = 0.06f;

        [Tooltip("Draw the eyes as wire balls on every cow when this object is selected in the Editor.")]
        [SerializeField] private bool _showGizmos = true;

        [Header("Look")]
        [Tooltip("The way the pupils look when Makey has no camera wired, along the cow object's own axes (this model's front is along -y). With a camera they look at it, whichever way the cow has turned. They wobble around the direction they look in.")]
        [SerializeField] private Vector3 _pupilDirection = new Vector3(0f, -1f, 0f);

        [Tooltip("The pupil's size, as a share of the eye's.")]
        [Range(0.05f, 0.9f)]
        [SerializeField] private float _pupilRatio = 0.45f;

        [Tooltip("How much the pupil shrinks when the eye is fully open, as a multiple of its size. Small is more terrified.")]
        [Range(0.1f, 1f)]
        [SerializeField] private float _terrifiedPupil = 0.6f;

        [Tooltip("How far out the pupil sits on the eyeball, as a share of the eye's radius. Just under 1 puts it on the surface.")]
        [Range(0.3f, 1f)]
        [SerializeField] private float _pupilReach = 0.85f;

        [Tooltip("How much the pupils wobble, from 0 (none) to 0.5 (a lot).")]
        [Range(0f, 0.5f)]
        [SerializeField] private float _wobble = 0.12f;

        [Tooltip("How fast the pupils wobble.")]
        [Min(0f)]
        [SerializeField] private float _wobbleSpeed = 22f;

        [Header("Motion")]
        [Tooltip("Seconds the eyes take to pop up when the cow is targeted.")]
        [Min(0f)]
        [SerializeField] private float _popSeconds = 0.2f;

        [Tooltip("How far the eyes swell past full size as they pop. 0 for none; about 1.7 swells them by 10%.")]
        [Min(0f)]
        [SerializeField] private float _popOvershoot = 1.7f;

        [Tooltip("Seconds the eyes take to shrink away when the cow is let go.")]
        [Min(0f)]
        [SerializeField] private float _deflateSeconds = 0.3f;

        [Header("Debug")]
        [Tooltip("Write to the Console when the eyes are made. Turn it off when it is no longer needed.")]
        [SerializeField] private bool _debugLog = true;

        // Two eyes per cow: the eye at index 2 * cow + side, side 0 for the left eye and 1 for the right.
        [NonSerialized] private Transform[] _eyeRoots;
        [NonSerialized] private Transform[] _balls;
        [NonSerialized] private Transform[] _pupils;

        // How far each cow's eyes have popped (0 to 1), how big they are drawn now (the pop swells past 1),
        // whether they are shown, and the cow's size in the world when the eyes were made, so the places
        // and sizes above can be in meters whatever the model's scale is.
        [NonSerialized] private float[] _levels;
        [NonSerialized] private float[] _sizes;
        [NonSerialized] private bool[] _shown;
        [NonSerialized] private Vector3[] _cowScales;

        [NonSerialized] private CowHerd _cowHerd;
        [NonSerialized] private Mesh _sphereMesh;
        [NonSerialized] private bool _captured;

        private void OnEnable()
        {
            _cowHerd = _makey != null ? _makey.CowHerd : null;
            if (_makey == null || _cowHerd == null)
            {
                Debug.LogError("CowEyesView needs a Makey that has a CowHerd wired in, so it will do nothing.", this);
                return;
            }

            if (_eyeWhiteMaterial == null || _pupilMaterial == null)
            {
                Debug.LogWarning("CowEyesView needs the white eyeball material and the black pupil material, so it will do nothing.", this);
                return;
            }

            // The built-in sphere mesh, without keeping the primitive or its collider.
            GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _sphereMesh = primitive.GetComponent<MeshFilter>().sharedMesh;
            Destroy(primitive);

            int count = _cowHerd.Count;
            _eyeRoots = new Transform[count * 2];
            _balls = new Transform[count * 2];
            _pupils = new Transform[count * 2];
            _levels = new float[count];
            _sizes = new float[count];
            _shown = new bool[count];
            _cowScales = new Vector3[count];

            for (int i = 0; i < count; i++)
            {
                Transform cow = _cowHerd.GetCow(i);
                if (cow == null)
                {
                    continue;
                }

                Vector3 scale = cow.lossyScale;
                _cowScales[i] = new Vector3(SafeScale(scale.x), SafeScale(scale.y), SafeScale(scale.z));

                for (int side = 0; side < 2; side++)
                {
                    int eye = i * 2 + side;
                    BuildEye(cow, i, eye, side == 0 ? "Cow eye left" : "Cow eye right");
                }
            }

            _captured = true;
            _cowHerd.HerdReset += OnHerdReset;

            if (_debugLog)
            {
                Debug.Log("[CowEyesView] Made eyes for " + count + " cows.", this);
            }
        }

        private void OnDisable()
        {
            if (!_captured)
            {
                return;
            }

            _captured = false;

            // The herd can already be destroyed when the scene shuts down.
            if (_cowHerd != null)
            {
                _cowHerd.HerdReset -= OnHerdReset;
            }

            for (int i = 0; i < _eyeRoots.Length; i++)
            {
                if (_eyeRoots[i] != null)
                {
                    Destroy(_eyeRoots[i].gameObject);
                }
            }

            _eyeRoots = null;
            _balls = null;
            _pupils = null;
        }

        // After the cow view has moved the cows, so the eyes never trail a frame behind them.
        private void LateUpdate()
        {
            if (!_captured)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            AbductionPhase phase = _makey.AbductionPhase;
            bool beamOn = phase == AbductionPhase.LoweringBeam || phase == AbductionPhase.Lifting;
            int target = beamOn ? _makey.TargetCowIndex : -1;
            Camera viewCamera = _makey.ViewCamera;

            for (int i = 0; i < _levels.Length; i++)
            {
                bool wanted = i == target;
                if (!wanted && _sizes[i] <= 0f)
                {
                    _levels[i] = 0f;
                    continue;
                }

                if (wanted)
                {
                    // Opening pops past full size and settles.
                    _levels[i] = Approach(_levels[i], 1f, deltaTime, _popSeconds);
                    _sizes[i] = CowMotion.PopScale(_levels[i], _popOvershoot);
                }
                else
                {
                    // Closing shrinks away from however big the eyes were, so a cow let go mid-pop does not jump.
                    _levels[i] = 0f;
                    _sizes[i] = Approach(_sizes[i], 0f, deltaTime, _deflateSeconds);
                }

                bool show = _sizes[i] > 0f;
                SetShown(i, show);
                if (show)
                {
                    float open = Mathf.Clamp01(_sizes[i]);
                    ApplyEye(i, i * 2, _leftEyeOffset, _sizes[i], open, viewCamera);
                    ApplyEye(i, i * 2 + 1, _rightEyeOffset, _sizes[i], open, viewCamera);
                }
            }
        }

        // A cow scale of zero would divide by zero below.
        private static float SafeScale(float scale)
        {
            return Mathf.Abs(scale) < 0.0001f ? 1f : scale;
        }

        // Moves a level towards a target at a steady rate, so the whole 0-to-1 range takes "seconds".
        private static float Approach(float level, float target, float deltaTime, float seconds)
        {
            if (seconds <= 0f)
            {
                return target;
            }

            return Mathf.MoveTowards(level, target, deltaTime / seconds);
        }

        private void BuildEye(Transform cow, int cowIndex, int eye, string eyeName)
        {
            GameObject root = new GameObject(eyeName);
            root.hideFlags = HideFlags.DontSave;
            root.layer = cow.gameObject.layer;
            root.transform.SetParent(cow, false);

            // The cow may be scaled up a lot (a model imported small). Scaling the eye's root back down makes
            // everything under it count in meters at the cow's normal size, and still follow her when she
            // shrinks or squashes.
            Vector3 scale = _cowScales[cowIndex];
            root.transform.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);

            _eyeRoots[eye] = root.transform;
            _balls[eye] = BuildPart(root.transform, "Eyeball", _eyeWhiteMaterial);
            _pupils[eye] = BuildPart(root.transform, "Pupil", _pupilMaterial);
            root.SetActive(false);
        }

        private Transform BuildPart(Transform parent, string partName, Material material)
        {
            GameObject part = new GameObject(partName);
            part.hideFlags = HideFlags.DontSave;
            part.layer = parent.gameObject.layer;
            part.transform.SetParent(parent, false);

            MeshFilter filter = part.AddComponent<MeshFilter>();
            filter.sharedMesh = _sphereMesh;

            MeshRenderer meshRenderer = part.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            return part.transform;
        }

        // Shows or hides both eyes of a cow, only when that changes.
        private void SetShown(int cowIndex, bool shown)
        {
            if (_shown[cowIndex] == shown)
            {
                return;
            }

            _shown[cowIndex] = shown;
            for (int side = 0; side < 2; side++)
            {
                Transform root = _eyeRoots[cowIndex * 2 + side];
                if (root != null)
                {
                    root.gameObject.SetActive(shown);
                }
            }
        }

        // Places one eye: the ball at its spot and size, and the pupil on the side of the ball that faces the way the cow looks.
        private void ApplyEye(int cowIndex, int eye, Vector3 offset, float size, float level, Camera viewCamera)
        {
            Transform root = _eyeRoots[eye];
            if (root == null)
            {
                return;
            }

            // The offset is in meters; the cow's own units are that many times smaller than her size.
            Vector3 scale = _cowScales[cowIndex];
            root.localPosition = new Vector3(offset.x / scale.x, offset.y / scale.y, offset.z / scale.z);
            float diameter = _eyeRadius * 2f * size;
            _balls[eye].localScale = new Vector3(diameter, diameter, diameter);

            // The pupils look at the camera, whichever way the cow has turned: the direction to the camera is put in
            // the eye's own axes (the root has the cow's turn). With no camera, the set direction is used.
            Vector3 facing;
            if (viewCamera != null)
            {
                Vector3 toCamera = viewCamera.transform.position - root.position;
                facing = toCamera.sqrMagnitude > 0.0001f ? root.InverseTransformDirection(toCamera.normalized) : Vector3.back;
            }
            else
            {
                facing = _pupilDirection.sqrMagnitude > 0.0001f ? _pupilDirection.normalized : Vector3.back;
            }

            // A wobble across the pupil's view, in two directions at right angles to the way it faces.
            Vector3 side = Vector3.Cross(facing, Vector3.up);
            if (side.sqrMagnitude < 0.0001f)
            {
                side = Vector3.Cross(facing, Vector3.right);
            }

            side.Normalize();
            Vector3 up = Vector3.Cross(side, facing);
            float phase = eye * 1.7f;
            float time = Time.time * _wobbleSpeed;
            Vector3 wobble = side * (Mathf.Sin(time + phase) * _wobble) + up * (Mathf.Cos(time * 1.3f + phase * 2f) * _wobble);
            Vector3 direction = (facing + wobble).normalized;

            float pupilDiameter = diameter * _pupilRatio * Mathf.Lerp(1f, _terrifiedPupil, level);
            _pupils[eye].localPosition = direction * (_eyeRadius * size * _pupilReach);
            _pupils[eye].localScale = new Vector3(pupilDiameter, pupilDiameter, pupilDiameter);
        }

        private void OnHerdReset()
        {
            for (int i = 0; i < _levels.Length; i++)
            {
                _levels[i] = 0f;
                _sizes[i] = 0f;
                SetShown(i, false);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!_showGizmos || _makey == null)
            {
                return;
            }

            CowHerd herd = _makey.CowHerd;
            if (herd == null)
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.9f, 0.3f, 1f);
            for (int i = 0; i < herd.Count; i++)
            {
                Transform cow = herd.GetCow(i);
                if (cow == null)
                {
                    continue;
                }

                // The offsets are in meters along the cow's own axes, whatever her scale.
                Gizmos.DrawWireSphere(cow.position + cow.rotation * _leftEyeOffset, _eyeRadius);
                Gizmos.DrawWireSphere(cow.position + cow.rotation * _rightEyeOffset, _eyeRadius);
            }
        }
    }
}
