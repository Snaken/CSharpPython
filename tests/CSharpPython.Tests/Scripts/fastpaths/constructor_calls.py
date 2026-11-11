class P:
    def __init__(self, x, y):
        self.x = x
        self.y = y

class Q(P):
    pass

class R(P):
    def __init__(self, x, y):
        super().__init__(x, y)
        self.z = x + y

class NoInit:
    pass

class Defaults:
    def __init__(self, a, b=2):
        self.a = a
        self.b = b

class Bad:
    def __init__(self):
        return 5

class Boom:
    def __init__(self, n):
        if n < 0:
            raise ValueError("negative")
        self.n = n

class Gen:
    def __init__(self):
        yield 1

p = P(1, 2)
print(p.x, p.y)
q = Q(3, 4)
print(q.x, q.y, type(q).__name__)
r = R(5, 6)
print(r.x, r.y, r.z)
print(type(NoInit()).__name__)
d = Defaults(1)
print(d.a, d.b)
d = Defaults(1, 3)
print(d.a, d.b)
d = Defaults(a=7)
print(d.a, d.b)

for args in [(), (1,), (1, 2, 3)]:
    try:
        P(*args)
    except TypeError:
        print("TypeError", len(args))

try:
    Bad()
except TypeError as e:
    print("TypeError")
try:
    Boom(-1)
except ValueError as e:
    print("ValueError", e)
print(Boom(2).n)
try:
    Gen()
except TypeError:
    print("TypeError gen")

# __init__ replaced or removed after the class was already used
def new_init(self, x, y):
    self.x = x * 10
    self.y = y * 10

print(P(1, 2).x)
P.__init__ = new_init
print(P(1, 2).x, Q(1, 2).x)
del P.__init__
print(type(P()).__name__)
try:
    P(1, 2)
except TypeError:
    print("TypeError no init")
Q.__init__ = new_init
print(Q(1, 2).x)
del Q.__init__
P.__init__ = lambda self, x, y: None
print(hasattr(P(1, 2), "x"))

# a constructor called in a loop, nested, and inside itself
class Node:
    def __init__(self, depth):
        self.depth = depth
        self.child = Node(depth - 1) if depth > 0 else None

n = Node(5)
count = 0
while n is not None:
    count += 1
    n = n.child
print(count)
total = 0
for i in range(200):
    total += Defaults(i, 1).a + Defaults(i).b
print(total)

# a closure default and a class built inside a function
def make(k):
    class C:
        def __init__(self, v):
            self.v = v + k
    return C

print(make(10)(1).v, make(20)(1).v)
