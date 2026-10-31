def g():
    try:
        yield 1
        yield 2
    finally:
        print('finally ran')
it = g()
print(next(it))
print(it.close())
print(list(it))
it.close()
