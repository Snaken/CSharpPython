class A:
    pass
a = A()
def ga(self, name):
    return 'dyn ' + name
A.__getattr__ = ga
print(a.foo)
