def f():
    x = 'outer'
    class K:
        x = 'inner'
        def g(self):
            return x
    return K().g(), K.x
print(f())
