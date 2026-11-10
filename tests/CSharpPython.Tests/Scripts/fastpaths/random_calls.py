import random
random.seed(12345)
print([random.randint(1, 6) for _ in range(20)])
print([random.randint(0, 0) for _ in range(3)], [random.randint(-5, -5) for _ in range(3)])
print([random.randint(-10, 10) for _ in range(10)])
print(random.randint(0, 2**31 - 2), random.randint(0, 2**31 - 1), random.randint(0, 2**31), random.randint(-2**40, 2**40))
print(random.randint(2**70, 2**70 + 5), random.randint(True, 3))
print(random.randint(a=1, b=100), random.randint(1, b=100), random.randint(b=50, a=40))
print([random.random() for _ in range(3)], random.uniform(1, 2), random.uniform(b=5, a=1))
print(random.choice([10, 20, 30, 40]), random.choice("abcdef"), random.choice(range(5, 500)), random.choice(seq=(1, 2, 3)))
x = list(range(20)); random.shuffle(x); print(x)
random.shuffle(x=x); print(x)
print(random.randrange(10), random.randrange(5, 50, 3), random.sample(range(100), 5), random.getrandbits(12))
print(random.gauss(), random.gauss(1, 2), random.gauss(sigma=3), random.expovariate())
for bad in (lambda: random.randint(5, 1), lambda: random.randint(1), lambda: random.randint(1, 2, 3),
            lambda: random.randint(1, a=2), lambda: random.randint(1, c=2), lambda: random.choice([]),
            lambda: random.random(1), lambda: random.random(x=1)):
    try:
        bad()
    except Exception as e:
        print(type(e).__name__)
