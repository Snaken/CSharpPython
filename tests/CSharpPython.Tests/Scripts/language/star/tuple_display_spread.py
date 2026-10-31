a = [1, 2]
print((*a, 3))
print((*a,))
print(*a, 3)
t = 0, *a
print(t, type(t).__name__)
print((*a, *a, *'ab'))
def f(*args):
    return args
print(f(*a, *a))
print((*range(3),))
