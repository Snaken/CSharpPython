g = 1
def f():
    g: int
    try:
        return g
    except UnboundLocalError:
        return 'unbound'
print(f())
def h():
    global g
    g = 2
h()
print(g)
