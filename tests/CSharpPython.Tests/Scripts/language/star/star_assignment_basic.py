a, *b = [1, 2, 3, 4]
print(a, b)
*a, b = [1, 2, 3, 4]
print(a, b)
a, *b, c = [1, 2, 3, 4]
print(a, b, c)
a, *b, c = [1, 2]
print(a, b, c)
a, b, *c = 'xy'
print(a, b, c)
first, *rest = range(5)
print(first, rest)
*init, last = (1,)
print(init, last)
