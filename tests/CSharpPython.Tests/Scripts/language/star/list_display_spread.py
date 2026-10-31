a = [1, 2]
b = (3, 4)
print([*a, 9])
print([0, *a, *b, 5])
print([*range(3), *'hi', *{'k': 1}, *{7}])
print([*a, *a])
print([*[]])
print([*(x * 2 for x in a)])
xs = [1]
ys = [*xs]
ys.append(2)
print(xs, ys)
