def a():
    x = 1
    def b():
        def c():
            nonlocal x
            x = 99
        c()
    b()
    return x
print(a())
