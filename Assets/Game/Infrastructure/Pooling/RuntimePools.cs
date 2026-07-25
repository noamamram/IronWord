using System;
using System.Collections.Generic;
using TypingDefense.Domain.Common;
using TypingDefense.Simulation.Interceptors;
using TypingDefense.Simulation.Threats;
using UnityEngine;
using UnityEngine.Pool;

namespace TypingDefense.Infrastructure.Pooling
{
    public interface IGameObjectPool<T> where T : Component
    {
        T Get();
        void Release(T instance);
    }

    public sealed class UnityComponentPool<T> : IGameObjectPool<T> where T : Component
    {
        private readonly ObjectPool<T> _pool;

        public UnityComponentPool(
            Func<T> create,
            Action<T> onGet = null,
            Action<T> onRelease = null,
            Action<T> onDestroy = null,
            int defaultCapacity = 16,
            int maxSize = 128)
        {
            _pool = new ObjectPool<T>(
                create,
                onGet,
                onRelease,
                onDestroy,
                collectionCheck: true,
                defaultCapacity: defaultCapacity,
                maxSize: maxSize);
        }

        public T Get() => _pool.Get();
        public void Release(T instance) => _pool.Release(instance);
    }

    public sealed class ThreatRuntimePool
    {
        private readonly Stack<ThreatRuntime> _pool = new Stack<ThreatRuntime>(32);
        private int _nextId = 1;

        public ThreatRuntime Get()
        {
            if (_pool.Count > 0)
                return _pool.Pop();

            return new ThreatRuntime(new ThreatId(_nextId++));
        }

        public void Release(ThreatRuntime threat)
        {
            if (threat == null)
                return;

            threat.ReturnToPool();
            _pool.Push(threat);
        }

        public void Clear()
        {
            _pool.Clear();
        }
    }

    public sealed class InterceptorRuntimePool
    {
        private readonly Stack<InterceptorRuntime> _pool = new Stack<InterceptorRuntime>(32);
        private int _nextId = 1;

        public InterceptorRuntime Get()
        {
            if (_pool.Count > 0)
                return _pool.Pop();

            return new InterceptorRuntime(new InterceptorId(_nextId++));
        }

        public void Release(InterceptorRuntime interceptor)
        {
            if (interceptor == null)
                return;

            interceptor.ReturnToPool();
            _pool.Push(interceptor);
        }

        public void Clear()
        {
            _pool.Clear();
        }
    }
}
