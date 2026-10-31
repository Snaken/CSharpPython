a = [1, 2, 3]
b = [4, 5]
del (a[0], b[0])
print(a, b)
x, y, z = 1, 2, 3
del (x, y), [z]
for n in ('x', 'y', 'z'):
    print(n in globals() if False else n)
del a[0], b
print(a)
