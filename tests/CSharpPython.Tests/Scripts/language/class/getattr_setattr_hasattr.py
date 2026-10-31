class A:
    c = 1
a = A()
setattr(a, 'x', 5)
print(getattr(a, 'x'), getattr(a, 'c'), getattr(a, 'zz', 'dflt'), hasattr(a, 'x'), hasattr(a, 'zz'), hasattr(a, 'c'))
setattr(A, 'later', 9)
print(a.later, getattr(A, 'later'), hasattr(A, 'later'), hasattr(A, 'nope'))
