# Attributes kept in slots must behave exactly like a dictionary: order, deletion, shadowing, many names.
class P:
    def __init__(self, x, y):
        self.x = x
        self.y = y

    def move(self):
        self.x += 1
        self.z = self.x + self.y
        return self.z

    def hello(self):
        return "hello"

a = P(1, 2)
b = P(3, 4)
print(vars(a), vars(b))
print(a.move(), vars(a), vars(b))
print([n for n in dir(a) if not n.startswith("__")], hasattr(a, "z"), hasattr(b, "z"), getattr(b, "z", "none"))

# the same class given attributes in another order
c = P(5, 6)
del c.x
c.x = 50
print(vars(c), list(vars(c)))
c.w = 1
print(list(vars(c)), c.x, c.y, c.w)
d = P(7, 8)
d.q = 1
d.p = 2
print(list(vars(d)), list(vars(P(0, 0))))

# delete and the error on a missing name
del d.q
print(list(vars(d)), hasattr(d, "q"))
for f in (lambda: d.q, lambda: delattr(d, "nope"), lambda: P(1, 2).nope):
    try:
        f()
    except AttributeError as e:
        print("AttributeError")

# setattr / getattr / delattr with computed names, and more names than a shape keeps
o = P(0, 0)
for i in range(40):
    setattr(o, "n" + str(i), i * i)
print(len(vars(o)), o.n0, o.n31, o.n32, o.n39, getattr(o, "n20"), list(vars(o))[:4], list(vars(o))[-2:])
o.x = 99
setattr(o, "n5", -5)
del o.n7
print(o.x, o.n5, hasattr(o, "n7"), len(vars(o)), o.move())

# an instance attribute shadows a method and a class variable, and deleting it brings them back
class Q:
    k = "class"
    def hello(self):
        return "method"
q = Q()
print(q.k, q.hello())
q.k = "instance"
q.hello = lambda: "shadowed"
print(q.k, q.hello(), sorted(vars(q)))
del q.k
del q.hello
print(q.k, q.hello(), vars(q))

# inheritance and super
class B:
    def __init__(self):
        self.base = 1
class C(B):
    def __init__(self):
        super().__init__()
        self.child = 2
class D(B):
    def __init__(self):
        self.mine = 0
        super().__init__()
print(vars(C()), vars(D()), vars(B()))

# a property that stores in the instance, and attributes set outside methods
class T:
    def __init__(self):
        self._v = 0
    @property
    def v(self):
        return self._v
    @v.setter
    def v(self, value):
        self._v = value * 2
t = T()
t.v = 4
t.extra = [1, 2]
t.extra.append(3)
print(t.v, vars(t))

# hooks
class H:
    def __init__(self):
        self.a = 1
    def __getattr__(self, name):
        return "missing " + name
    def __setattr__(self, name, value):
        object.__setattr__(self, name, value + 1)
h = H()
h.b = 10
print(h.a, h.b, h.c, vars(h))

# exceptions keep their attributes and arguments
class MyErr(Exception):
    def __init__(self, msg, code):
        super().__init__(msg)
        self.code = code
try:
    try:
        raise ValueError("inner")
    except ValueError as inner:
        raise MyErr("outer", 7) from inner
except MyErr as e:
    print(e.args, e.code, str(e), repr(e.__cause__), vars(e).get("code"))

# many instances of different shapes in a list, then sorted by an attribute
items = []
for i in range(30):
    it = P(i, 30 - i)
    if i % 3 == 0:
        it.tag = "t" + str(i)
    if i % 5 == 0:
        it.big = i * 100
    items.append(it)
items.sort(key=lambda it: it.y)
print([it.x for it in items[:5]], sum(1 for it in items if hasattr(it, "tag")), sum(getattr(it, "big", 0) for it in items))
print(len({tuple(vars(it)) for it in items}))
