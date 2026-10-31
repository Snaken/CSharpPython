# list methods

xs = [1, 2]
xs.extend(xs)
print(xs)

xs = [1, 2, 3]
xs.insert(1, 9)
xs.insert(-1, 8)
xs.insert(100, 7)
xs.insert(-100, 6)
print(xs)

xs = [1, 2, 3]
print(xs.pop(), xs.pop(0), xs)

xs = [1, 2, 3]
print(xs.pop(-2), xs)

xs = [1, 2, 1]
xs.remove(1)
print(xs)

xs = [1, 2, 3, 2]
print(xs.index(2), xs.index(2, 2), xs.count(2), xs.count(9))

xs = [3, 1, 2]
xs.sort()
print(xs)

xs = [3, 1, 2]
xs.sort(reverse=True)
print(xs)

xs = ['b', 'a']
xs.sort()
print(xs)

xs = [1, 2, 3]
xs.reverse()
print(xs)

xs = [1, 2]
xs.clear()
print(xs, len(xs))

xs = [1, [2]]
ys = xs.copy()
ys.append(3)
ys[1].append(9)
print(xs, ys)

xs = [1, 2, 3]
xs[0] = 'a'
xs[-1] = 'z'
print(xs)

xs = [1, 2, 3]
xs[1] += 10
print(xs)

xs = [[0] * 2] * 2
xs[0][0] = 1
print(xs)

xs = [1, 2, 3, 4]
del_ok = xs.pop()
print(del_ok, xs)

xs = []
for i in range(5):
    xs.append(i * i)
print(xs)

xs = [1, 2, 3]
ys = xs
ys.append(4)
print(xs is ys, xs == ys, xs)

a = [1]
a.append(a)
print(a)

d = {}
d['self'] = d
print(d)

xs = [5, 3, 8, 1]
xs.sort()
xs.reverse()
print(xs, xs.index(8))

xs = [1.5, 1, 2]
xs.sort()
print(xs)

xs = [True, 0, 2]
xs.sort()
print(xs)

xs = [(2, 'b'), (1, 'z'), (2, 'a')]
xs.sort()
print(xs)

xs = [3, 1, 2]
print(xs.sort(), xs)

print([1, 2].append(3))

xs = [1, 2, 3]
for x in xs:
    print(x)
print(xs)
