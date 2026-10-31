def g():
    try:
        yield 1
    finally:
        print('never')
it = g()
it.close()
print(list(it))
it2 = g()
next(it2)
it2.close()
it2.close()
print('ok')
