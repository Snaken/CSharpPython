class A:
    def f(self): pass
r = repr(A().f)
print(r.startswith('<bound method'), 'A.f' in r or 'f of' in r)
