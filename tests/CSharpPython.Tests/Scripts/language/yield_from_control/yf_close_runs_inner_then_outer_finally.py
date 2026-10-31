def inner():
    try:
        yield 1
    finally:
        print('inner finally')
def outer():
    try:
        yield from inner()
    finally:
        print('outer finally')
g = outer()
next(g)
print(g.close())
try:
    next(g)
except StopIteration:
    print('closed')
