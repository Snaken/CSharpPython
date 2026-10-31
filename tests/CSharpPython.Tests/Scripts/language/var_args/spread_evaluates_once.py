log = []
def src():
    log.append(1)
    return [1, 2]
def f(a, b):
    return a + b
print(f(*src()), log)
