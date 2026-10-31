a = list(range(10))
del a[2:4]
print(a)
del a[:2]
print(a)
del a[-2:]
print(a)
b = list(range(10))
del b[::2]
print(b)
c = list(range(10))
del c[::-3]
print(c)
d = list(range(6))
del d[1:5:2]
print(d)
e = list(range(5))
del e[:]
print(e)
f = [1, 2, 3]
del f[5:9]
del f[2:1]
print(f)
