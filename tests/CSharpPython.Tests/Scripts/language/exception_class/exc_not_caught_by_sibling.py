class A(Exception):
    pass
class B(Exception):
    pass
try:
    try:
        raise A('a')
    except B:
        print('wrong')
except A as e:
    print('right', e)
