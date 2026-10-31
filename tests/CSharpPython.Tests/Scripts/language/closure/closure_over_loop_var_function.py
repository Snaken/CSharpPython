def f():
    out = []
    for i in range(3):
        def g(i=i):
            return i * i
        out.append(g)
    return [out[0](), out[1](), out[2]()]
print(f())
