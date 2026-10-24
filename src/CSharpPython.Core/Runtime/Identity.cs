namespace CSharpPython
{
    internal static partial class Ops
    {
        /// <summary>Python's <c>is</c>: true when both values are the same object (or equal immediates).</summary>
        public static bool Is(Value a, Value b)
        {
            if (a.Kind != b.Kind) return false;
            switch (a.Kind)
            {
                case ValueKind.None:
                case ValueKind.Unbound:
                    return true;
                case ValueKind.Bool:
                case ValueKind.Int:
                case ValueKind.Float:
                    return a.RawInt == b.RawInt;
                case ValueKind.Handle:
                    return a.RawAux == b.RawAux && a.RawInt == b.RawInt;
                case ValueKind.Str:
                    return string.Equals((string)a.RawObject, (string)b.RawObject, System.StringComparison.Ordinal);
                default:
                    return ReferenceEquals(a.RawObject, b.RawObject);
            }
        }
    }
}
