using System;
using TypingDefense.Domain.Typing;
using UnityEngine.InputSystem;

namespace TypingDefense.Infrastructure.Input
{
    /// <summary>
    /// Reads physical key presses and maps them through IPhysicalTextMapper.
    /// Ignores OS keyboard language / onTextInput characters.
    /// </summary>
    public sealed class UnityTextInputAdapter : ITextInputSource, IDisposable
    {
        private static readonly Key[] TrackedKeys =
        {
            Key.A, Key.B, Key.C, Key.D, Key.E, Key.F, Key.G, Key.H, Key.I, Key.J,
            Key.K, Key.L, Key.M, Key.N, Key.O, Key.P, Key.Q, Key.R, Key.S, Key.T,
            Key.U, Key.V, Key.W, Key.X, Key.Y, Key.Z,
            Key.Semicolon, Key.Comma, Key.Period, Key.Slash, Key.Quote
        };

        public event Action<string> TextCommitted;
        public event Action BackspacePressed;

        private bool _enabled = true;
        private IPhysicalTextMapper _mapper;

        public UnityTextInputAdapter(IPhysicalTextMapper mapper = null)
        {
            _mapper = mapper ?? new EnglishPhysicalTextMapper();
        }

        public bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        public void SetMapper(IPhysicalTextMapper mapper)
        {
            _mapper = mapper ?? new EnglishPhysicalTextMapper();
        }

        public void Bind()
        {
            // Physical-key polling only — no OS text input subscription.
        }

        public void Dispose()
        {
        }

        public void Tick()
        {
            if (!_enabled)
                return;

            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.backspaceKey.wasPressedThisFrame)
                BackspacePressed?.Invoke();

            for (var i = 0; i < TrackedKeys.Length; i++)
            {
                var key = TrackedKeys[i];
                if (!keyboard[key].wasPressedThisFrame)
                    continue;

                if (_mapper.TryMapKey(key, out var text) && !string.IsNullOrEmpty(text))
                    TextCommitted?.Invoke(text);
            }
        }
    }

    /// <summary>
    /// Deterministic input source for EditMode / PlayMode tests.
    /// </summary>
    public sealed class FakeTextInputSource : ITextInputSource
    {
        public event Action<string> TextCommitted;
        public event Action BackspacePressed;

        public void Submit(string text) => TextCommitted?.Invoke(text);
        public void PressBackspace() => BackspacePressed?.Invoke();
    }
}
