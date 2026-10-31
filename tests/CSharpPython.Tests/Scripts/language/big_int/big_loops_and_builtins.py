f = 1
for i in range(1, 31):
    f *= i
print(f)
a, b = 0, 1
for i in range(200):
    a, b = b, a + b
print(a)
print(sum(range(10**6)) * 10**10, sum([10**18] * 100), 2**64 % 1000, 10**25 // 10**20)
x = 1
while x < 10**40:
    x = x * 7 + 3
print(x, x.__class__.__name__ if False else type(x).__name__, isinstance(x, int), isinstance(x, float), type(x) is int, type(x) == int, isinstance(x, (int, str)), isinstance(True, int))
print(bool(2**64), bool(0), not 2**64, 2**64 and 'yes', 0 or 2**64)
print([2**64] * 2, (2**64,), {2**64: 2**65}, {2**64, 2**65} == {2**65, 2**64})
print(list(map(lambda v: v * 2**62, [1, 2, 3])), [v ** 2 for v in (2**32, 2**33)])
n = 5
n **= 40
n <<= 3
n -= 1
n //= 3
n %= 10**20
n |= 2**70
n ^= 1
n &= ~2
n >>= 2
print(n)
print(f'{10**20:,} {2**70:x} {2**70:>30} {-2**70:+} {2**70:e}')
print(int.__name__ if False else 'skip', (2**64).__class__ if False else 'skip')
