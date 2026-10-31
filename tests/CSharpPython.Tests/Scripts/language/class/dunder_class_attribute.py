class A: pass
class B(A): pass
b = B()
print(b.__class__ is B, b.__class__.__name__, B.__name__)
