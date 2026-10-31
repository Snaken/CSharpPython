def f(*a, **k):
    return a, k
a = [1, 2]
d = {'k': 1}
print(f(*a, *a, **d, **{'j': 2}))
def g():
    return *a, 3
print(g())
print(max(*a, 5))
print([*a, *f(*a)[0]])
x = [*a, *[*a, 0]]
print(x)
print({**d, **{**d, 'n': 1}})
