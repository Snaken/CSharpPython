def f():
    total = 0
    def add(v):
        nonlocal total
        total += v
    for i in range(5):
        add(i)
    return total
print(f())
