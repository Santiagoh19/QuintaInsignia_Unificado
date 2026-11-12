using System;
namespace RecorridoAR
{
    // Accept only fresh hand samples. Leaving the target or losing the hand cancels the hold.
    public sealed class DwellGate
    {
        float held;
        bool fired;
        public float Duration = .8f;
        public float Progress => Math.Min(1, held / Math.Max(.1f, Duration));
        // True after confirming, until the finger leaves (prevents repeated confirmations).
        public bool Fired => fired;
        public void Reset() { held = 0; fired = false; }
        public bool Sample(bool inside, float seconds)
        {
            if (!inside) { Reset(); return false; }
            if (fired) return false;
            held += Math.Max(0, Math.Min(.1f, seconds));
            if (held < Math.Max(.1f, Duration)) return false;
            fired = true;
            return true;
        }
    }
}
