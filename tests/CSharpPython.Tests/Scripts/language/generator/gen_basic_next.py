def count(n):
    i = 0
    while i < n:
        yield i
        i += 1
g = count(3)
print(next(g), next(g), next(g))
try:
    next(g)
except StopIteration:
    print('stop')
try:
    next(g)
except StopIteration:
    print('still stopped')
print(next(g, 'default'))
