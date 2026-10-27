# raise ... from, __cause__, __context__ and __suppress_context__
def describe(e):
    if e is None:
        return "None"
    return type(e).__name__ + "(" + str(e) + ")"


def show(e):
    print(describe(e), "cause:", describe(e.__cause__), "context:", describe(e.__context__), "suppress:", e.__suppress_context__)


class AppError(Exception):
    pass


class Detail(Exception):
    def __init__(self, code):
        super().__init__("code " + str(code))
        self.code = code


# An explicit cause
try:
    try:
        raise ValueError("inner")
    except ValueError as inner:
        raise RuntimeError("outer") from inner
except RuntimeError as e:
    show(e)

# From a class, from None, and from an instance of a script class
try:
    raise AppError("a") from ValueError
except AppError as e:
    show(e)

try:
    raise AppError("b") from None
except AppError as e:
    show(e)

try:
    raise ValueError("plain")
except ValueError as e:
    show(e)

try:
    raise RuntimeError("x") from Detail(7)
except RuntimeError as e:
    show(e)
    print(e.__cause__.code)

# The context is the exception being handled
try:
    try:
        1 / 0
    except ZeroDivisionError:
        raise LookupError("k")
except LookupError as e:
    show(e)

try:
    try:
        [][3]
    except IndexError:
        {}["missing"]
except LookupError as e:
    show(e)

# The context of an error the VM raises
try:
    try:
        raise ValueError("first")
    except ValueError:
        int("nope")
except ValueError as e:
    show(e)
    show(e.__context__)

# A raise inside a function called from an except block
def fail():
    raise AppError("from function")


try:
    try:
        raise ValueError("handled")
    except ValueError:
        fail()
except AppError as e:
    show(e)

# From and the context together: the cause is kept and the context is still recorded
try:
    try:
        raise ValueError("one")
    except ValueError as one:
        raise TypeError("two") from LookupError("cause")
except TypeError as e:
    show(e)

# Instances of script classes keep the chain too
try:
    try:
        raise Detail(1)
    except Detail as d:
        raise AppError("wrapped") from d
except AppError as e:
    show(e)
    print(e.__cause__.code, e.__cause__ is not None)

# The handled exception is forgotten when the block ends
try:
    try:
        raise ValueError("done")
    except ValueError:
        pass
    raise LookupError("later")
except LookupError as e:
    show(e)

for i in range(3):
    try:
        raise ValueError("loop " + str(i))
    except ValueError:
        if i == 1:
            continue
        if i == 2:
            break
try:
    raise OSError("after the loop")
except OSError as e:
    show(e)

# An inner try inside an except block gives the outer exception back when it ends
try:
    try:
        raise ValueError("outer one")
    except ValueError:
        try:
            raise LookupError("inner one")
        except LookupError:
            pass
        raise RuntimeError("after inner")
except RuntimeError as e:
    show(e)

try:
    try:
        raise ValueError("outer two")
    except ValueError:
        try:
            raise LookupError("inner two")
        except LookupError:
            pass
        raise
except ValueError as e:
    show(e)

# Raising in an except block, caught further out, then raising again
try:
    try:
        try:
            raise ValueError("a1")
        except ValueError:
            raise LookupError("a2")
    except LookupError:
        pass
    raise IndexError("a3")
except IndexError as e:
    show(e)

# A bare raise keeps the exception as it was
try:
    try:
        raise ValueError("same")
    except ValueError:
        raise
except ValueError as e:
    show(e)

# Raising the exception being handled does not make it its own context
try:
    try:
        raise ValueError("self")
    except ValueError as e:
        raise e
except ValueError as e:
    show(e)

# A chain can be walked
def chain(e):
    names = []
    while e is not None:
        names.append(type(e).__name__)
        e = e.__cause__ or e.__context__
    return names


try:
    try:
        try:
            raise LookupError("c1")
        except LookupError as c1:
            raise ValueError("c2") from c1
    except ValueError:
        raise RuntimeError("c3")
except RuntimeError as e:
    print(chain(e))

# finally
def cleanup():
    try:
        try:
            raise ValueError("body")
        finally:
            raise LookupError("in finally")
    except LookupError as e:
        show(e)


cleanup()

# with: an error raised while leaving the block carries the one that was in flight
class Closer:
    def __enter__(self):
        return self

    def __exit__(self, kind, value, trace):
        raise AppError("closing")


try:
    with Closer():
        raise ValueError("inside with")
except AppError as e:
    show(e)

# A generator that lets StopIteration escape
def leaky():
    yield 1
    raise StopIteration("stop")


try:
    list(leaky())
except RuntimeError as e:
    show(e)

# Bad causes
for bad in (5, "text", object):
    try:
        raise ValueError("v") from bad
    except TypeError as e:
        print(e)

try:
    raise 5 from ValueError("x")
except TypeError as e:
    print(e)

# The error that was handled before an exception in a loop does not leak into the next one
results = []
for n in range(3):
    try:
        try:
            raise ValueError(n)
        except ValueError as v:
            if n == 1:
                raise KeyError(n)
    except LookupError as k:
        results.append(describe(k.__context__))
    else:
        results.append("no error")
print(results)
