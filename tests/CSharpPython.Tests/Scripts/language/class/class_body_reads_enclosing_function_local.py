def f(a):
    class K:
        v = a * 2
    return K.v
print(f(21))
