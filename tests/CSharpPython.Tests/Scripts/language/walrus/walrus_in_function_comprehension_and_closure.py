def f(data):
    out = [(prev := x) for x in data]
    def inner():
        return prev
    return out, prev, inner()
print(f([5, 6, 7]))
def g(data):
    total = 0
    seen = [total := total + x for x in data]
    return seen, total
print(g([1, 2, 3]))
