cache = {}
def put(k, v):
    cache[k] = v
def drop(k):
    if k in cache:
        del cache[k]
put('a', 1)
put('b', 2)
drop('a')
drop('zz')
print(cache)
data = [3, 1, 2]
tmp = sorted(data)
del tmp
print(data)
