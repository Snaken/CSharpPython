def walk(tree):
    if isinstance(tree, list):
        for t in tree:
            yield from walk(t)
    else:
        yield tree
print(list(walk([1, [2, [3, 4]], 5, [[6]]])))
def fib():
    a, b = 0, 1
    while True:
        yield a
        a, b = b, a + b
g = fib()
print([next(g) for i in range(10)])
