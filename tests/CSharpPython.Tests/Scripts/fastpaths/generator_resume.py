# a generator resumed from a for loop and from next() in turn
def count(n):
    for i in range(n):
        yield i

g = count(6)
out = []
for x in g:
    out.append(x)
    if x == 1:
        break
out.append(next(g))
for x in g:
    out.append(x)
print(out)

# try/finally and try/except kept open across a yield
def guarded():
    try:
        yield 1
        yield 2
    finally:
        print("cleanup")

for v in guarded():
    print("got", v)

def catcher():
    for i in range(3):
        try:
            yield i
            if i == 1:
                raise ValueError("boom")
        except ValueError as e:
            print("caught", e)

print(list(catcher()))

# send, throw and close
def echo():
    received = []
    try:
        while True:
            value = yield len(received)
            received.append(value)
    except KeyError:
        yield "thrown"
    finally:
        print("echo done", received)

e = echo()
print(next(e), e.send("a"), e.send("b"))
print(e.throw(KeyError))
e.close()

# a generator that carries an exception being handled across a yield
def during_handler():
    try:
        raise ValueError("outer")
    except ValueError:
        yield "in handler"
        yield "still in handler"

print(list(during_handler()))

# nested and delegated generators
def inner(n):
    for i in range(n):
        yield i * 10

def outer():
    yield "start"
    yield from inner(3)
    for x in inner(2):
        yield x + 1
    return "done"

print(list(outer()))

# the same generator function used by two loops at once, and a generator as a closure
def make(k):
    def gen(n):
        for i in range(n):
            yield i + k
    return gen

a = make(100)(3)
b = make(200)(3)
print([(x, y) for x, y in zip(a, b)])
print(list(make(1)(2)), list(make(2)(2)))

# a method that is a generator, and one that has a default argument
class Bag:
    def __init__(self, items):
        self.items = items
    def each(self, scale=1):
        for item in self.items:
            yield item * scale

bag = Bag([1, 2, 3])
print(list(bag.each()), list(bag.each(10)), sum(bag.each(2)))

# many generators alive at once, resumed alternately
gens = [count(3) for _ in range(4)]
rounds = []
for _ in range(3):
    rounds.append([next(g) for g in gens])
print(rounds)

# recursion through generators
def walk(depth):
    if depth > 0:
        yield depth
        yield from walk(depth - 1)

print(list(walk(6)))

# next with a default, and an exhausted generator
g = count(1)
print(next(g, "none"), next(g, "none"), next(g, "none"))
for _ in g:
    print("never")
