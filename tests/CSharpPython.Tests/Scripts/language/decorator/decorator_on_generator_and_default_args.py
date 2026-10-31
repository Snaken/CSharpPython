def keep(fn):
    return fn
@keep
def gen(n=3):
    for i in range(n):
        yield i
print(list(gen()), list(gen(2)))
