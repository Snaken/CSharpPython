def loud(fn):
    def w(self, *a):
        return fn(self, *a).upper()
    return w
class A:
    @loud
    def hi(self, name): return 'hi ' + name
print(A().hi('bob'))
