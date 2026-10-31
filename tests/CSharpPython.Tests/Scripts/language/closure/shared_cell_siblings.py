def make():
    n = 0
    def inc():
        nonlocal n
        n += 1
    def get():
        return n
    return inc, get
inc, get = make()
inc()
inc()
print(get())
