def f():
    for i in range(10):
        if i % 2:
            continue
        if i > 6:
            break
        yield i
    else_value = 'after'
    yield else_value
print(list(f()))
def pairs():
    for i in range(3):
        for j in range(3):
            if j > i:
                break
            yield (i, j)
print(list(pairs()))
def w():
    n = 0
    while n < 5:
        n += 1
        if n == 2:
            continue
        yield n
print(list(w()))
