total = 0
def adder(xs):
    global total
    for x in xs:
        total += x
        yield total
print(list(adder([1, 2, 3])), total)
def counter():
    n = 0
    def bump():
        nonlocal n
        n += 1
        return n
    while True:
        yield bump()
c = counter()
print(next(c), next(c), next(c))
