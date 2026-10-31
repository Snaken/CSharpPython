def g():
    try:
        for i in range(10):
            yield i
    finally:
        print('cleanup at', i)
it = g()
for x in it:
    if x == 2:
        break
it.close()
print('after close')
