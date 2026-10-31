class A: pass
class B: pass
class C(A): pass
print(isinstance(C(), (B, A)), isinstance(B(), (A, C)), isinstance(1, (A, int)))
