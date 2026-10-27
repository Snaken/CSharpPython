# The wording of CPython 3.14 for errors that used to read differently
def attempt(label, fn):
    try:
        fn()
    except Exception as e:
        print(label, type(e).__name__ + ":", e)


attempt("int /", lambda: 1 / 0)
attempt("int //", lambda: 1 // 0)
attempt("int %", lambda: 1 % 0)
attempt("float /", lambda: 1.0 / 0)
attempt("float //", lambda: 1.0 // 0)
attempt("float %", lambda: 1.0 % 0)
attempt("divmod", lambda: divmod(1, 0))
attempt("divmod float", lambda: divmod(1.0, 0))
attempt("big /", lambda: (2 ** 70) / 0)
attempt("big //", lambda: (2 ** 70) // 0)
attempt("big %", lambda: (2 ** 70) % 0)
attempt("0 ** -1", lambda: 0 ** -1)
attempt("0.0 ** -1", lambda: 0.0 ** -1)
attempt("pow(2, 3, 0)", lambda: pow(2, 3, 0))


def local():
    print(x)
    x = 1


def deleted():
    y = 1
    del y
    return y


def outer():
    def inner():
        return z
    result = inner()
    z = 1
    return result


attempt("local", local)
attempt("deleted", deleted)
attempt("free", outer)

# int() with base given as a keyword
print(int("101", base=2), int("ff", base=16), int("z", base=36), int("0x1f", base=0))
print(int("  42  ", 10), int("-7", base=8))
for bad in (lambda: int(5, base=2), lambda: int(base=2), lambda: int("1", radix=2), lambda: int("1", base=1), lambda: int("9", base=8)):
    attempt("int", bad)
