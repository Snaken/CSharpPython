class A: pass
print(bool(A()), 'yes' if A() else 'no', not A())
