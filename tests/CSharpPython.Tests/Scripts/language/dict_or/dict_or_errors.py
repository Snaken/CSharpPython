def t(f):
    try:
        print(f())
    except Exception as e:
        print(type(e).__name__, e)
t(lambda: {1: 1} | [(2, 2)])
t(lambda: {1: 1} | 5)
t(lambda: [(2, 2)] | {1: 1})
x = {1: 1}
try:
    x |= 5
except TypeError as e:
    print(e)
try:
    x |= [(1, 2, 3)]
except Exception as e:
    print(type(e).__name__, e)
t(lambda: {1: 1} & {1: 1})
t(lambda: {1: 1} ^ {1: 1})
