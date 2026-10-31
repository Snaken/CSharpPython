calls = []
def noisy(x):
    calls.append(x)
    return x
g = (noisy(i) for i in range(3))
print(calls)
print(next(g), calls)
print(list(g), calls)
print(list(g))
total = sum(x * 2 for x in range(4))
print(total)
first = next(i for i in range(10) if i > 4)
print(first)
