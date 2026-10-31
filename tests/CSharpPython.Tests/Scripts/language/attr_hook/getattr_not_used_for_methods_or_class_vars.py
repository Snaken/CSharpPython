class A:
    k = 1
    def m(self): return 'm'
    def __getattr__(self, name): return 'fallback ' + name
a = A()
print(a.k, a.m(), a.q)
print(A.k)
