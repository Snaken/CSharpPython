k = 3
def f():
    def h():
        return k
    return h()
print(f())
