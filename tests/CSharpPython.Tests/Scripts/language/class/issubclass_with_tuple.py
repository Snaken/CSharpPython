class A: pass
class B(A): pass
class C: pass
print(issubclass(B, (C, A)), issubclass(C, (A, B)), issubclass(A, A))
