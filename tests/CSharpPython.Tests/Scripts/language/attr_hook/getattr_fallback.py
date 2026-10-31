class A:
    def __init__(self):
        self.real = 1
    def __getattr__(self, name):
        print('getattr', name)
        return name.upper()
a = A()
print(a.real)
print(a.fake)
print(getattr(a, 'other'), hasattr(a, 'zzz'))
a.fake2 = 5
print(a.fake2)
