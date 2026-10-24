using static CSharpPython.Tests.Runtime.Rt;

namespace CSharpPython.Tests.Runtime
{
    public class ValueTests
    {
        [Test]
        public void DefaultValueIsNone()
        {
            Assert.That(default(Value).IsNone, Is.True);
            Assert.That(Value.None.Kind, Is.EqualTo(ValueKind.None));
        }

        [Test]
        public void KindsAndPredicates()
        {
            Assert.That(Value.FromBool(true).IsBool, Is.True);
            Assert.That(Value.True.AsBool(), Is.True);
            Assert.That(Value.False.AsBool(), Is.False);
            Assert.That(Value.FromInt(3).IsInt, Is.True);
            Assert.That(Value.FromFloat(1.5).IsFloat, Is.True);
            Assert.That(Value.FromString("x").IsString, Is.True);
            Assert.That(L(1).IsList, Is.True);
            Assert.That(T(1).IsTuple, Is.True);
            Assert.That(D().IsDict, Is.True);
            Assert.That(Value.FromHandle(1, 2).IsHandle, Is.True);
            Assert.That(Value.FromInt(1).IsNumber && Value.True.IsNumber && Value.FromFloat(1).IsNumber, Is.True);
            Assert.That(Value.FromString("x").IsNumber, Is.False);
        }

        [Test]
        public void NullStringBecomesNone() => Assert.That(Value.FromString(null).IsNone, Is.True);

        [Test]
        public void ImplicitConversions()
        {
            Value a = 5, b = 5L, c = 2.5, d = true, e = "hi";
            Assert.That(a.Kind, Is.EqualTo(ValueKind.Int));
            Assert.That(b.AsInt(), Is.EqualTo(5));
            Assert.That(c.AsFloat(), Is.EqualTo(2.5));
            Assert.That(d.Kind, Is.EqualTo(ValueKind.Bool));
            Assert.That(e.AsString(), Is.EqualTo("hi"));
        }

        [Test]
        public void AccessorsConvertNumbers()
        {
            Assert.That(Value.True.AsInt(), Is.EqualTo(1));
            Assert.That(Value.FromInt(3).AsFloat(), Is.EqualTo(3.0));
            Assert.That(Value.True.AsFloat(), Is.EqualTo(1.0));
        }

        [Test]
        public void AccessorsRaiseTypeErrorOnWrongKind()
        {
            Assert.That(Raises(() => Value.FromString("x").AsInt()).TypeName, Is.EqualTo("TypeError"));
            Assert.That(Raises(() => Value.FromFloat(1.5).AsInt()).Problem, Does.Contain("expected an int, got float"));
            Assert.That(Raises(() => Value.None.AsFloat()).Problem, Does.Contain("expected a number"));
            Assert.That(Raises(() => Value.FromInt(1).AsString()).Problem, Does.Contain("expected a str"));
            Assert.That(Raises(() => Value.FromInt(1).AsList()).Problem, Does.Contain("expected a list"));
            Assert.That(Raises(() => Value.FromInt(1).AsTuple()).Problem, Does.Contain("expected a tuple"));
            Assert.That(Raises(() => Value.FromInt(1).AsDict()).Problem, Does.Contain("expected a dict"));
            Assert.That(Raises(() => { var _ = Value.FromInt(1).HandleType; }).Problem, Does.Contain("expected a handle"));
            Assert.That(Raises(() => { var _ = Value.FromInt(1).HandleId; }).Problem, Does.Contain("expected a handle"));
        }

        [Test]
        public void CollectionAccessors()
        {
            Assert.That(L(1, 2).AsList().Count, Is.EqualTo(2));
            Assert.That(T(1, 2, 3).AsTuple().Count, Is.EqualTo(3));
            Assert.That(D(1, 2).AsDict().Count, Is.EqualTo(1));
        }

        [Test]
        public void HandlesCarryTypeAndId()
        {
            var h = Value.FromHandle(7, 123456789012L);
            Assert.That(h.HandleType, Is.EqualTo(7));
            Assert.That(h.HandleId, Is.EqualTo(123456789012L));
            Assert.That(h.TypeName, Is.EqualTo("handle"));
            Assert.That(h.Repr(), Is.EqualTo("<handle 7#123456789012>"));
            Assert.That(h == Value.FromHandle(7, 123456789012L), Is.True);
            Assert.That(h == Value.FromHandle(8, 123456789012L), Is.False);
            Assert.That(h == Value.FromHandle(7, 1), Is.False);
            Assert.That(h.GetHashCode(), Is.EqualTo(Value.FromHandle(7, 123456789012L).GetHashCode()));
            Assert.That(h.IsTruthy, Is.True);
        }

        [Test]
        public void PythonEqualitySemantics()
        {
            Assert.That(Value.FromInt(1) == Value.FromFloat(1.0), Is.True);
            Assert.That(Value.FromInt(1) == Value.True, Is.True);
            Assert.That(Value.FromInt(0) == Value.False, Is.True);
            Assert.That(Value.FromInt(1) != Value.FromInt(2), Is.True);
            Assert.That(Value.FromInt(1) == Value.FromString("1"), Is.False);
            Assert.That(Value.None == default(Value), Is.True);
            Assert.That(Value.None == Value.FromInt(0), Is.False);
            Assert.That(Value.FromString("a") == Value.FromString("a"), Is.True);
            Assert.That(Value.FromFloat(double.NaN) == Value.FromFloat(double.NaN), Is.False);
            Assert.That(Value.FromInt(1).Equals((object)Value.FromFloat(1.0)), Is.True);
            Assert.That(Value.FromInt(1).Equals("x"), Is.False);
            Assert.That(Value.FromInt(1).Equals(Value.FromInt(1)), Is.True);
        }

        [Test]
        public void HashesAgreeWithEquality()
        {
            Assert.That(Value.FromInt(1).GetHashCode(), Is.EqualTo(Value.FromFloat(1.0).GetHashCode()));
            Assert.That(Value.FromInt(1).GetHashCode(), Is.EqualTo(Value.True.GetHashCode()));
            Assert.That(Value.FromString("ab").GetHashCode(), Is.EqualTo(Value.FromString("ab").GetHashCode()));
            Assert.That(T(1, "a").GetHashCode(), Is.EqualTo(T(1, "a").GetHashCode()));
            Assert.That(Value.None.GetHashCode(), Is.EqualTo(0));
            Assert.That(Value.FromFloat(1.5).GetHashCode(), Is.EqualTo(Value.FromFloat(1.5).GetHashCode()));
            Assert.That(Range(0, 3).GetHashCode(), Is.EqualTo(Range(0, 3).GetHashCode()));
            var f = Builtin("len");
            Assert.That(f.GetHashCode(), Is.EqualTo(f.GetHashCode()));
        }

        [Test]
        public void UnhashableTypesRaise()
        {
            Assert.That(Raises(() => L(1).GetHashCode()).Problem, Is.EqualTo("unhashable type: 'list'"));
            Assert.That(Raises(() => D().GetHashCode()).Problem, Is.EqualTo("unhashable type: 'dict'"));
            Assert.That(Raises(() => Ops.MakeSlice(Value.None, Value.None, Value.None).GetHashCode()).Problem, Does.Contain("slice"));
        }

        [Test]
        public void TruthinessFollowsPython()
        {
            Assert.That(Value.None.IsTruthy, Is.False);
            Assert.That(Value.FromInt(0).IsTruthy, Is.False);
            Assert.That(Value.FromInt(-1).IsTruthy, Is.True);
            Assert.That(Value.FromFloat(0.0).IsTruthy, Is.False);
            Assert.That(Value.FromFloat(0.1).IsTruthy, Is.True);
            Assert.That(Value.FromString("").IsTruthy, Is.False);
            Assert.That(Value.FromString("a").IsTruthy, Is.True);
            Assert.That(L().IsTruthy, Is.False);
            Assert.That(L(0).IsTruthy, Is.True);
            Assert.That(T().IsTruthy, Is.False);
            Assert.That(D().IsTruthy, Is.False);
            Assert.That(D(1, 2).IsTruthy, Is.True);
            Assert.That(Range(0, 0).IsTruthy, Is.False);
            Assert.That(Range(0, 1).IsTruthy, Is.True);
            Assert.That(Builtin("len").IsTruthy, Is.True);
        }

        [Test]
        public void TypeNames()
        {
            Assert.That(Value.None.TypeName, Is.EqualTo("NoneType"));
            Assert.That(Value.True.TypeName, Is.EqualTo("bool"));
            Assert.That(Value.FromInt(1).TypeName, Is.EqualTo("int"));
            Assert.That(Value.FromFloat(1).TypeName, Is.EqualTo("float"));
            Assert.That(Value.FromString("").TypeName, Is.EqualTo("str"));
            Assert.That(L().TypeName, Is.EqualTo("list"));
            Assert.That(T().TypeName, Is.EqualTo("tuple"));
            Assert.That(D().TypeName, Is.EqualTo("dict"));
            Assert.That(Range(0, 1).TypeName, Is.EqualTo("range"));
            Assert.That(Builtin("len").TypeName, Is.EqualTo("builtin_function_or_method"));
            Assert.That(Builtin("ValueError").TypeName, Is.EqualTo("type"));
            Assert.That(Value.FromModule(Modules["math"]).TypeName, Is.EqualTo("module"));
            Assert.That(Ops.MakeSlice(Value.None, Value.None, Value.None).TypeName, Is.EqualTo("slice"));
            Assert.That(Value.Unbound.TypeName, Is.EqualTo("unbound"));
        }

        [Test]
        public void ToStringIsPythonStrAndReprIsPythonRepr()
        {
            Assert.That(Value.FromString("hi").ToString(), Is.EqualTo("hi"));
            Assert.That(Value.FromString("hi").Repr(), Is.EqualTo("'hi'"));
            Assert.That(Value.FromInt(5).ToString(), Is.EqualTo("5"));
        }

        [Test]
        public void ConvenienceConstructors()
        {
            Assert.That(R(Value.List(1, 2)), Is.EqualTo("[1, 2]"));
            Assert.That(R(Value.Tuple(1, 2)), Is.EqualTo("(1, 2)"));
        }

        [Test]
        public void RangeLengthAndIndexing()
        {
            Assert.That(new PyRange(0, 10, 3).Length, Is.EqualTo(4));
            Assert.That(new PyRange(10, 0, -3).Length, Is.EqualTo(4));
            Assert.That(new PyRange(5, 5, 1).Length, Is.EqualTo(0));
            Assert.That(new PyRange(5, 0, 1).Length, Is.EqualTo(0));
            Assert.That(new PyRange(0, 5, -1).Length, Is.EqualTo(0));
            Assert.That(new PyRange(0, 10, 3)[2], Is.EqualTo(6));
            Assert.That(new PyRange(long.MinValue, long.MaxValue, long.MaxValue).Length, Is.EqualTo(3));
        }

        [Test]
        public void PyErrorCarriesTypeAndMessage()
        {
            var e = new PyError("ValueError", "bad");
            Assert.That(e.TypeName, Is.EqualTo("ValueError"));
            Assert.That(e.Problem, Is.EqualTo("bad"));
            Assert.That(e.Message, Is.EqualTo("ValueError: bad"));
        }

        [Test]
        public void ExceptionTypesFormAHierarchy()
        {
            Assert.That(ExceptionTypes.ZeroDivisionError.IsSubclassOf(ExceptionTypes.ArithmeticError), Is.True);
            Assert.That(ExceptionTypes.ZeroDivisionError.IsSubclassOf(ExceptionTypes.Exception), Is.True);
            Assert.That(ExceptionTypes.ZeroDivisionError.IsSubclassOf(ExceptionTypes.ZeroDivisionError), Is.True);
            Assert.That(ExceptionTypes.ValueError.IsSubclassOf(ExceptionTypes.TypeError), Is.False);
            Assert.That(ExceptionTypes.KeyError.IsSubclassOf(ExceptionTypes.LookupError), Is.True);
            Assert.That(ExceptionTypes.IndexError.IsSubclassOf(ExceptionTypes.LookupError), Is.True);
            Assert.That(ExceptionTypes.RecursionError.IsSubclassOf(ExceptionTypes.RuntimeError), Is.True);
            Assert.That(ExceptionTypes.UnboundLocalError.IsSubclassOf(ExceptionTypes.NameError), Is.True);
            Assert.That(ExceptionTypes.ValueError.ToString(), Is.EqualTo("ValueError"));
            Assert.That(ExceptionTypes.OverflowError.Parent, Is.EqualTo(ExceptionTypes.ArithmeticError));
        }

        [Test]
        public void ExceptionTypeRegistry()
        {
            Assert.That(ExceptionTypes.TryGet("ValueError", out var v), Is.True);
            Assert.That(v, Is.EqualTo(ExceptionTypes.ValueError));
            Assert.That(ExceptionTypes.TryGet("NoSuchThingXyz", out _), Is.False);
            var custom = ExceptionTypes.GetOrCreate("RegistryTestError");
            Assert.That(custom.Parent, Is.EqualTo(ExceptionTypes.Exception));
            Assert.That(ExceptionTypes.GetOrCreate("RegistryTestError"), Is.SameAs(custom));
            var child = ExceptionTypes.Register("RegistryChildError", custom);
            Assert.That(child.IsSubclassOf(custom), Is.True);
            var orphan = ExceptionTypes.Register("RegistryOrphanError", null);
            Assert.That(orphan.Parent, Is.EqualTo(ExceptionTypes.Exception));
            Assert.That(ExceptionTypes.All, Does.Contain(child));
        }

        [Test]
        public void ExceptionValueFormatting()
        {
            var e = new PyExceptionValue(ExceptionTypes.ValueError, "bad");
            Assert.That(e.ToString(), Is.EqualTo("ValueError: bad"));
            Assert.That(new PyExceptionValue(ExceptionTypes.ValueError, null).ToString(), Is.EqualTo("ValueError"));
            Assert.That(new TraceFrame("f", 3).ToString(), Is.EqualTo("line 3, in f"));
            var v = Value.FromException(e);
            Assert.That(v.Repr(), Is.EqualTo("ValueError('bad')"));
            Assert.That(v.ToString(), Is.EqualTo("bad"));
            Assert.That(v.TypeName, Is.EqualTo("ValueError"));
            Assert.That(Value.FromException(new PyExceptionValue(ExceptionTypes.ValueError, "")).Repr(), Is.EqualTo("ValueError()"));
        }
    }
}
