class A: pass
print(str(A) == repr(A), repr(A).startswith('<class '), repr(A).endswith("A'>"))
