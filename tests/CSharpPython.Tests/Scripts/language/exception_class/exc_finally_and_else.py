class E(Exception):
    pass
def f(n):
    try:
        if n:
            raise E('bad')
    except E as e:
        print('except', e)
        return 'e'
    else:
        print('else')
        return 'ok'
    finally:
        print('finally')
print(f(1), f(0))
