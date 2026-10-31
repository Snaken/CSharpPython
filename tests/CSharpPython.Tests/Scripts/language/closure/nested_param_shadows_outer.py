def f():
    x = 'outer'
    def h(x):
        return x
    return h('inner'), x
print(f())
