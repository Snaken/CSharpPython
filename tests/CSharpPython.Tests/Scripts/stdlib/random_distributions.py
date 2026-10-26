import random

# The results use sin, cos and log, which differ in the last bits between platforms, so they are shown to 9 digits.
def show(values):
    return ' '.join('%.9g' % v for v in values)

def t(f):
    try:
        r = f()
        print(type(r).__name__, '%.9g' % r)
    except Exception as e:
        print(type(e).__name__)

random.seed(100)
print(show([random.uniform(1, 2) for i in range(5)]))
print(show([random.uniform(10, -10) for i in range(5)]))
print(show([random.uniform(0, 1e10) for i in range(3)]))
print(show([random.triangular() for i in range(5)]))
print(show([random.triangular(0, 10) for i in range(5)]))
print(show([random.triangular(0, 10, 2) for i in range(5)]))
print(show([random.triangular(5, 5) for i in range(2)]))
print(show([random.triangular(low=1, high=3, mode=3) for i in range(4)]))
print(show([random.gauss() for i in range(6)]))
print(show([random.gauss(10, 2) for i in range(6)]))
print(show([random.gauss(mu=5, sigma=0.5) for i in range(3)]))
print(show([random.normalvariate() for i in range(6)]))
print(show([random.normalvariate(100, 15) for i in range(4)]))
print(show([random.lognormvariate(0, 1) for i in range(4)]))
print(show([random.expovariate() for i in range(5)]))
print(show([random.expovariate(0.5) for i in range(5)]))
print(show([random.expovariate(-2) for i in range(2)]))

# gauss makes values in pairs: reseeding drops the saved second value.
random.seed(5)
a = random.gauss()
random.seed(5)
b = random.gauss()
c = random.gauss()
random.seed(5)
d = random.gauss()
e = random.gauss()
print(a == b == d, c == e, '%.9g %.9g' % (a, c))

t(lambda: random.expovariate(0))
t(lambda: random.uniform(1))
t(lambda: random.uniform('a', 'b'))
t(lambda: random.gauss('a'))
t(lambda: random.triangular(0, 1, 'x'))
t(lambda: random.lognormvariate(1))
