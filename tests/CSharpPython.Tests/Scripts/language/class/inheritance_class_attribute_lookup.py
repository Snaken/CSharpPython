class A:
    v = 'a'
class B(A):
    pass
class C(B):
    v = 'c'
print(A.v, B.v, C.v)
A.v = 'changed'
print(A.v, B.v, C.v)
