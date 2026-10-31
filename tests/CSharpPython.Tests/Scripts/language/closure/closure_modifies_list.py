def f():
    xs = []
    def add(v):
        xs.append(v)
    add(1)
    add(2)
    return xs
print(f())
