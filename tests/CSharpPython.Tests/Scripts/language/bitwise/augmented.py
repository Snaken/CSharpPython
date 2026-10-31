x = 12
x &= 10
print(x)
x |= 1
print(x)
x ^= 15
print(x)
x <<= 3
print(x)
x >>= 2
print(x)
a = [5, 6]
a[0] |= 2
a[1] <<= 1
print(a)
class H:
    pass
h = H()
h.f = 1
h.f <<= 4
h.f ^= 1
print(h.f)
