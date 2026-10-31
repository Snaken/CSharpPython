def g():
    try:
        try:
            yield 1
        finally:
            print('inner finally')
    except ValueError:
        print('outer except')
        yield 'handled'
    finally:
        print('outer finally')
it = g()
next(it)
print(it.throw(ValueError()))
it.close()
print('end')
