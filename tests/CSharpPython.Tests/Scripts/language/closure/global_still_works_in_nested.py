g = 1
def f():
    def h():
        global g
        g = 5
    h()
f()
print(g)
