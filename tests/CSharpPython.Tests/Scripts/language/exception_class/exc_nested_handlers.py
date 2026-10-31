class A(Exception):
    pass
class B(A):
    pass
def f():
    try:
        raise B('inner')
    except B as e:
        raise A('outer')
try:
    f()
except A as e:
    print(type(e).__name__, e)
