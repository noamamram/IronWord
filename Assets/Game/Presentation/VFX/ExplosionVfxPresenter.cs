using System.Collections;
using System.Collections.Generic;
using TypingDefense.Application.Events;
using TypingDefense.Domain.Common;
using UnityEngine;

namespace TypingDefense.Presentation.VFX
{
    public sealed class ExplosionVfxPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject _explosionPrefab;
        [SerializeField] private GameObject _launchExplosionPrefab;
        [SerializeField] private float _interceptScale = 0.9f;
        [SerializeField] private float _impactScale = 1.35f;
        [SerializeField] private float _launchScale = 0.55f;
        [SerializeField] private float _effectLifetimeSeconds = 3.5f;
        [SerializeField] private float _launchEffectLifetimeSeconds = 2f;
        [SerializeField] private float _minHeight = 2f;

        private GameplayEventBus _events;
        private Transform _root;
        private bool _subscribed;
        private readonly HashSet<int> _recentThreatIds = new HashSet<int>();

        public void Configure(GameObject explosionPrefab, GameObject launchExplosionPrefab = null)
        {
            _explosionPrefab = explosionPrefab;
            if (launchExplosionPrefab != null)
                _launchExplosionPrefab = launchExplosionPrefab;
        }

        public void SetScales(float interceptScale, float impactScale, float launchScale = -1f)
        {
            _interceptScale = Mathf.Max(0.05f, interceptScale);
            _impactScale = Mathf.Max(0.05f, impactScale);
            if (launchScale > 0f)
                _launchScale = Mathf.Max(0.05f, launchScale);
        }

        public void Initialize(GameplayEventBus events)
        {
            if (this == null)
                return;

            Shutdown();
            _events = events;

            if (_root == null)
            {
                var rootGo = new GameObject("ExplosionVfx");
                rootGo.transform.SetParent(transform, false);
                _root = rootGo.transform;
            }

            if (_events == null)
                return;

            _events.ThreatIntercepted += OnIntercepted;
            _events.ThreatImpacted += OnImpacted;
            _events.InterceptorLaunched += OnLaunched;
            _subscribed = true;
        }

        public void Shutdown()
        {
            // OnDestroy order can destroy this presenter before GameInstaller.Teardown runs.
            if (this == null)
                return;

            StopAllCoroutines();
            _recentThreatIds.Clear();

            if (_subscribed && _events != null)
            {
                _events.ThreatIntercepted -= OnIntercepted;
                _events.ThreatImpacted -= OnImpacted;
                _events.InterceptorLaunched -= OnLaunched;
            }

            _subscribed = false;
            _events = null;
        }

        private void OnDestroy()
        {
            // Unsubscribe without touching StopAllCoroutines after destroy.
            if (_subscribed && _events != null)
            {
                _events.ThreatIntercepted -= OnIntercepted;
                _events.ThreatImpacted -= OnImpacted;
                _events.InterceptorLaunched -= OnLaunched;
            }

            _subscribed = false;
            _events = null;
            _recentThreatIds.Clear();
        }

        private void OnIntercepted(ThreatId id, Vector3 worldPosition)
        {
            if (this == null)
                return;
            Play(_explosionPrefab, id.Value, worldPosition, _interceptScale, _effectLifetimeSeconds, raiseToMinHeight: true);
        }

        private void OnImpacted(ThreatId id, Vector3 worldPosition)
        {
            if (this == null)
                return;
            Play(_explosionPrefab, id.Value, worldPosition, _impactScale, _effectLifetimeSeconds, raiseToMinHeight: true);
        }

        private void OnLaunched(ThreatId threatId, InterceptorId interceptorId, Vector3 worldPosition)
        {
            if (this == null)
                return;

            // Offset key so launch flashes never collide with intercept/impact dedupe ids.
            var launchKey = unchecked(0x40000000 + interceptorId.Value);
            Play(
                _launchExplosionPrefab,
                launchKey,
                worldPosition,
                _launchScale,
                _launchEffectLifetimeSeconds,
                raiseToMinHeight: false,
                namePrefix: "LaunchFlash_");
        }

        private void Play(
            GameObject prefab,
            int idKey,
            Vector3 worldPosition,
            float scale,
            float lifetime,
            bool raiseToMinHeight,
            string namePrefix = "Explosion_")
        {
            if (this == null || prefab == null)
                return;

            if (!_recentThreatIds.Add(idKey))
                return;

            StartCoroutine(ClearRecentId(idKey));

            var spawnPos = worldPosition;
            if (raiseToMinHeight && spawnPos.y < _minHeight)
                spawnPos.y = _minHeight;

            var parent = _root != null ? _root : transform;
            var instance = Instantiate(prefab, spawnPos, Quaternion.identity, parent);
            instance.name = namePrefix + idKey;
            instance.transform.localScale = Vector3.one * scale;
            PrepareOneShotParticles(instance);
            Destroy(instance, lifetime);
        }

        private IEnumerator ClearRecentId(int threatId)
        {
            yield return new WaitForSeconds(0.35f);
            _recentThreatIds.Remove(threatId);
        }

        private static void PrepareOneShotParticles(GameObject instance)
        {
            var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (var i = 0; i < systems.Length; i++)
            {
                var system = systems[i];
                var main = system.main;
                main.loop = false;
                main.playOnAwake = false;

                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                system.Play(true);
            }
        }
    }
}
