def a():
    x = 1
    def b():
        def c():
            def d():
                return x
            return d()
        return c()
    return b()
print(a())
