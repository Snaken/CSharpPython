class A:
    def f(self, a, b):
        return a
for call in (lambda: A().f(1), lambda: A().f(), lambda: A().f(1, 2, 3), lambda: A().f(1, c=2)):
    try:
        call()
    except TypeError:
        print('TypeError')
