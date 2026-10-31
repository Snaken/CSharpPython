class A: pass
class B(A): pass
print(type(B()) == B, type(B()) == A, type(A()) is not type(B()))
