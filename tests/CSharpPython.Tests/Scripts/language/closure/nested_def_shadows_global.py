x = 'g'
def f():
    x = 'l'
    def h():
        return x
    return h()
print(f(), x)
