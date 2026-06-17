using System;
using System.Collections.Generic;

namespace ACSDlg.Core
{
    /// <summary>
    /// Computes the reveal progress of ONE line. Stateful cursor (current speed, instant mode,
    /// remaining pause time) advanced by the deltaTime it receives. Passive: never reads the
    /// clock itself, so it is deterministic and testable with a fixed delta. Neutral: returns a
    /// character count and the emits crossed this tick; the View applies them. One instance per View.
    /// </summary>
    public class TypewriterAnimator
    {
        private static readonly string[] NoEmits = new string[0];

        /// <summary>Seconds per character at speed 1.0. The View configures it (its "base reading speed").</summary>
        public float BaseSecondsPerCharacter = 0.03f;

        private ParsedLine? _line;
        private int _visibleCount;
        private int _tagIndex;
        private float _speedMultiplier;
        private bool _instant;
        private float _pauseRemaining;
        private float _charTimeAccumulated;
        private List<string>? _pendingEmits; // emits crossed by Skip(), delivered on the next Update()

        /// <summary>Arms the animator on a pre-parsed line. Resets the whole cursor state.</summary>
        public void Start(ParsedLine pLine)
        {
            _line = pLine;
            _visibleCount = 0;
            _tagIndex = 0;
            _speedMultiplier = 1f;
            _instant = false;
            _pauseRemaining = 0f;
            _charTimeAccumulated = 0f;
            _pendingEmits = null;
        }

        /// <summary>
        /// Consumes ALL of the given delta in an internal loop: it can cross several characters,
        /// several emits and a pause boundary in the same call (framerate independent).
        /// Returns the new visible character count and the emits crossed this tick.
        /// </summary>
        public TypewriterTick Update(float pDeltaTime)
        {
            List<string>? lEmits = _pendingEmits;
            _pendingEmits = null;

            if (_line == null)
                return new TypewriterTick(0, lEmits ?? (IReadOnlyList<string>)NoEmits);

            string lText = _line.CleanText;
            float lRemaining = pDeltaTime;

            while (true)
            {
                // Fire every tag sitting at the cursor position (before the character at that index).
                while (_tagIndex < _line.Tags.Count && _line.Tags[_tagIndex].Position == _visibleCount)
                {
                    MarkupTag lTag = _line.Tags[_tagIndex++];
                    switch (lTag.Kind)
                    {
                        case MarkupKind.Speed:
                            _instant = false;
                            _speedMultiplier = lTag.NumericValue;
                            break;
                        case MarkupKind.Teleport:
                            _instant = true;
                            break;
                        case MarkupKind.Pause:
                            _pauseRemaining += lTag.NumericValue;
                            break;
                        case MarkupKind.Emit:
                            (lEmits ??= new List<string>()).Add(lTag.StringValue!);
                            break;
                    }
                }

                // A pending pause consumes time before anything else moves.
                if (_pauseRemaining > 0f)
                {
                    if (lRemaining < _pauseRemaining)
                    {
                        _pauseRemaining -= lRemaining;
                        break;
                    }
                    lRemaining -= _pauseRemaining;
                    _pauseRemaining = 0f;
                }

                if (_visibleCount >= lText.Length)
                    break;

                if (_instant)
                {
                    _visibleCount++;
                    continue;
                }

                float lCharCost = _speedMultiplier <= 0f ? 0f : BaseSecondsPerCharacter / _speedMultiplier;
                if (lCharCost <= 0f)
                {
                    _visibleCount++;
                    continue;
                }

                if (_charTimeAccumulated + lRemaining < lCharCost)
                {
                    _charTimeAccumulated += lRemaining;
                    break;
                }

                lRemaining -= lCharCost - _charTimeAccumulated;
                _charTimeAccumulated = 0f;
                _visibleCount++;
            }

            return new TypewriterTick(_visibleCount, lEmits ?? (IReadOnlyList<string>)NoEmits);
        }

        /// <summary>
        /// Reveals everything and marks the remaining emits; they are delivered by the next
        /// Update() call (single delivery channel — the View loop does not change).
        /// </summary>
        public void Skip()
        {
            if (_line == null)
                return;

            for (; _tagIndex < _line.Tags.Count; _tagIndex++)
                if (_line.Tags[_tagIndex].Kind == MarkupKind.Emit)
                    (_pendingEmits ??= new List<string>()).Add(_line.Tags[_tagIndex].StringValue!);

            _visibleCount = _line.CleanText.Length;
            _pauseRemaining = 0f;
            _charTimeAccumulated = 0f;
        }

        /// <summary>True when the line is fully revealed (including trailing pauses and pending emits).</summary>
        public bool GetAnimationDone()
        {
            return _line != null
                && _visibleCount >= _line.CleanText.Length
                && _pauseRemaining <= 0f
                && _tagIndex >= _line.Tags.Count
                && _pendingEmits == null;
        }
    }

    /// <summary>
    /// Result of one animator tick. The View applies <see cref="VisibleCharacterCount"/>
    /// (TMP maxVisibleCharacters / Substring) and relays <see cref="Emits"/> to the engine.
    /// </summary>
    public struct TypewriterTick
    {
        public int VisibleCharacterCount;
        public IReadOnlyList<string> Emits;

        public TypewriterTick(int pVisibleCharacterCount, IReadOnlyList<string> pEmits)
        {
            VisibleCharacterCount = pVisibleCharacterCount;
            Emits = pEmits;
        }
    }
}
