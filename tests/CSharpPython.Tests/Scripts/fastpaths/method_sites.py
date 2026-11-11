class A:
    def __init__(self, v):
        self.v = v
    def get(self):
        return self.v
    def twice(self):
        return self.get() * 2

class B(A):
    def get(self):
        return self.v + 100

class C(A):
    pass

def call_get(o):
    return o.get()

# one call site that sees several classes in turn
objs = [A(1), B(2), C(3), A(4), B(5)]
for _ in range(3):
    print([call_get(o) for o in objs])
print([o.twice() for o in objs])

# an attribute that shadows the method, on one object only
a = A(1)
b = A(2)
print(call_get(a), call_get(b))
b.get = lambda: "shadow"
print(call_get(a), call_get(b))
del b.get
print(call_get(b))

# an object whose attributes were added in another order has another shape
class D:
    def __init__(self, first):
        if first:
            self.x = 1
            self.y = 2
        else:
            self.y = 2
            self.x = 1
    def m(self):
        return self.x + self.y

print([D(i % 2 == 0).m() for i in range(6)])

# the method is replaced, added and removed on the class after the site was used
print(call_get(A(7)))
A.get = lambda self: "replaced"
print(call_get(A(7)), call_get(B(7)), call_get(C(7)))
del B.get
print(call_get(B(7)))
B.get = lambda self: "again"
print(call_get(B(7)))
del A.get
try:
    call_get(C(1))
except AttributeError:
    print("AttributeError")
A.get = lambda self: "back"
print(call_get(C(1)))

# an instance attribute added after the site was used, and the object in dictionary mode
class E:
    def f(self):
        return "method"
e = E()
def call_f(o):
    return o.f()
print(call_f(e))
e.f = lambda: "attribute"
print(call_f(e))
e2 = E()
e2.k = 1
del e2.k
print(call_f(e2))
e2.f = lambda: "dict attribute"
print(call_f(e2))

# a method that is a staticmethod, a classmethod and a property-like callable attribute
class F:
    @staticmethod
    def s():
        return "static"
    @classmethod
    def c(cls):
        return cls.__name__
    def __init__(self):
        self.cb = lambda: "cb"
f = F()
for _ in range(2):
    print(f.s(), f.c(), f.cb())

# many attributes (dictionary mode) and a __getattr__ hook
class G:
    def __getattr__(self, name):
        return lambda: "hook " + name
    def real(self):
        return "real"
g = G()
for _ in range(2):
    print(g.real(), g.other())
