class K:
    x = 1
    def f(self):
        return x
try:
    K().f()
except NameError:
    print('NameError')
