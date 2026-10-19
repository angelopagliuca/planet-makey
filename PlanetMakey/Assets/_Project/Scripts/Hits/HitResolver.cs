using System;
using PlanetMakey.Core;
using UnityEngine;

namespace PlanetMakey.Hits
{
    /// <summary>
    /// Turns each hit into a ray through the camera and tells the target it lands on. The
    /// projected picture is the camera's view, so a position on the wall is a point in the
    /// camera's viewport.
    /// </summary>
    public sealed class HitResolver : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("Every hit source to resolve hits from.")]
        [SerializeField] private HitSource[] _sources;

        [Tooltip("The camera whose view is projected onto the wall.")]
        [SerializeField] private Camera _camera;

        [Header("Raycast")]
        [Tooltip("Layers a ball can hit.")]
        [SerializeField] private LayerMask _layers = ~0;

        [Tooltip("How far into the scene a ball can reach, in meters.")]
        [SerializeField] private float _maxDistance = 100f;

        /// <summary>
        /// Raised once for every hit this resolver handles, whether it landed on a target, on
        /// something that is not a target, or on nothing. Nothing is raised while the resolver has
        /// no camera.
        /// </summary>
        public event Action<ResolvedHit> HitResolved;

        private void OnEnable()
        {
            if (_camera == null)
            {
                Debug.LogWarning("HitResolver has no camera, so it cannot resolve any hits.", this);
            }

            if (_sources == null || _sources.Length == 0)
            {
                Debug.LogWarning(
                    "HitResolver has no hit sources, so it will not resolve any hits. " +
                    "Add the hit sources to its Sources list.",
                    this);
                return;
            }

            for (int i = 0; i < _sources.Length; i++)
            {
                if (_sources[i] != null)
                {
                    _sources[i].HitReceived += OnHitReceived;
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

        private void OnHitReceived(BallHit hit)
        {
            if (_camera == null)
            {
                return;
            }

            // Outside 0 to 1 is off the projected picture. Written so that NaN also counts as off.
            Vector2 position = hit.Position;
            bool onPicture = position.x >= 0f && position.x <= 1f
                && position.y >= 0f && position.y <= 1f;
            if (!onPicture)
            {
                HitResolved?.Invoke(new ResolvedHit(hit, null, null, Vector3.zero));
                return;
            }

            Ray ray = _camera.ViewportPointToRay(new Vector3(position.x, position.y, 0f));

            // Triggers are ignored so an invisible volume, such as a beam, cannot swallow a ball.
            if (!Physics.Raycast(ray, out RaycastHit info, _maxDistance, _layers, QueryTriggerInteraction.Ignore))
            {
                HitResolved?.Invoke(new ResolvedHit(hit, null, null, Vector3.zero));
                return;
            }

            // The nearest target up the parent chain wins, so a target can own several colliders.
            IBallTarget target = info.collider.GetComponentInParent<IBallTarget>();
            if (target != null)
            {
                target.ReceiveBall(hit.Color, info.point);
            }

            HitResolved?.Invoke(new ResolvedHit(hit, info.collider, target, info.point));
        }
    }
}
