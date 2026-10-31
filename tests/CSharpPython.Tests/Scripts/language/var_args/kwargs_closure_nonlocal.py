def f(**k):
    def g():
        nonlocal k
        k = {'z': 1}
    g()
    return k
print(f(a=1))
