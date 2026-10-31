class A:
    def f(self):
        raise ValueError('bad')
try:
    A().f()
except ValueError as e:
    print('caught', e)
