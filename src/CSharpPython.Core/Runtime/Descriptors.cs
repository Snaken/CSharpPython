namespace CSharpPython
{
    internal enum DescriptorKind : byte
    {
        StaticMethod,
        ClassMethod,
        Property,
    }

    /// <summary>
    /// What <c>staticmethod</c>, <c>classmethod</c> and <c>property</c> make. Stored in a class body, it changes what
    /// looking the name up on the class or on an instance gives.
    /// </summary>
    internal sealed class PyDescriptor
    {
        /// <summary>
        /// Set once any property exists, so that attribute access on instances only pays for looking the class up
        /// when a property could be in the way. It never goes back to false.
        /// </summary>
        public static bool PropertiesExist;

        public readonly DescriptorKind Kind;

        /// <summary>The wrapped function of a staticmethod or classmethod.</summary>
        public readonly Value Function;

        /// <summary>The functions of a property; None where it has none.</summary>
        public readonly Value Getter, Setter, Deleter;

        public PyDescriptor(DescriptorKind kind, Value function, Value getter = default, Value setter = default, Value deleter = default)
        {
            Kind = kind;
            Function = function;
            Getter = getter;
            Setter = setter;
            Deleter = deleter;
            if (kind == DescriptorKind.Property) PropertiesExist = true;
        }

        public string TypeName =>
            Kind == DescriptorKind.StaticMethod ? "staticmethod" : Kind == DescriptorKind.ClassMethod ? "classmethod" : "property";

        /// <summary>A property with one of its functions replaced, which is what <c>@x.setter</c> makes.</summary>
        public PyDescriptor With(Value? getter, Value? setter, Value? deleter) =>
            new PyDescriptor(DescriptorKind.Property, default, getter ?? Getter, setter ?? Setter, deleter ?? Deleter);
    }
}
