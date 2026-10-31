def make():
    count = 0
    def counted(fn):
        def w(*a):
            nonlocal count
            count += 1
            return fn(*a)
        return w
    @counted
    def add(a, b): return a + b
    add(1, 2)
    add(3, 4)
    return add, lambda: count
add, count = make()
add(5, 6)
print(count())
