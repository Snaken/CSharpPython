namespace CSharpPython
{
    /// <summary>The answer to "may this output be wired to that input?", with the reason when it may not.</summary>
    public readonly struct PinCheck
    {
        public bool Allowed { get; }

        /// <summary>Why the wire is refused, in words an editor can show. Null when it is allowed.</summary>
        public string Reason { get; }

        private PinCheck(bool allowed, string reason)
        {
            Allowed = allowed;
            Reason = reason;
        }

        internal static PinCheck Yes => new PinCheck(true, null);

        internal static PinCheck No(string reason) => new PinCheck(false, reason);
    }

    /// <summary>
    /// The rules for wiring an output pin to an input pin. An editor checks each wire with this as it is drawn, and
    /// the compiler checks every wire of a graph with it, so a graph with a bad wire never compiles.
    /// </summary>
    /// <remarks>
    /// <c>any</c> (and an unresolved type variable) fits on either side, because an untyped host function gives <c>any</c> pins. An <c>int</c> fits a
    /// <c>float</c> pin, as in Python. Every other mismatch is refused, including <c>bool</c> to <c>int</c> and
    /// <c>float</c> to <c>int</c>. Lists must have the same element type (or <c>any</c> on one side), because a
    /// list can be changed through either name. Handle types fit only the same handle type.
    /// </remarks>
    public static class PinCompatibility
    {
        /// <summary>Checks a wire from an output of type <paramref name="from"/> to an input of type <paramref name="to"/>.</summary>
        public static PinCheck Check(PinType from, PinType to)
        {
            if (from == null || to == null) throw new System.ArgumentNullException(from == null ? nameof(from) : nameof(to));
            if (from.Equals(PinType.None)) return PinCheck.No("A pin of type none has no value to wire.");
            if (IsOpen(from) || IsOpen(to)) return PinCheck.Yes;
            if (from.Equals(to)) return PinCheck.Yes;
            if (from.Equals(PinType.Int) && to.Equals(PinType.Float)) return PinCheck.Yes;

            if (from.IsList && to.IsList)
            {
                if (IsOpen(from.Element) || IsOpen(to.Element)) return PinCheck.Yes;
                return PinCheck.No("A wire of type " + from + " cannot go to a pin of type " + to + ": a list keeps its own element type.");
            }

            return PinCheck.No("A wire of type " + from + " cannot go to a pin of type " + to + ".");
        }

        // An unresolved type variable fits anything, like any.
        private static bool IsOpen(PinType t) => t.Equals(PinType.Any) || t.IsVariable;

        /// <summary>True when <see cref="Check"/> allows the wire.</summary>
        public static bool CanConnect(PinType from, PinType to) => Check(from, to).Allowed;
    }
}
