using System;

namespace CSharpPython
{
    /// <summary>
    /// A task could not be saved (it holds something that cannot be written, such as a host function that was
    /// never registered) or a snapshot could not be restored (it is damaged, from another version, or was taken
    /// from a different script).
    /// </summary>
    public sealed class SnapshotException : Exception
    {
        public SnapshotException(string message) : base(message) { }

        public SnapshotException(string message, Exception inner) : base(message, inner) { }
    }
}
