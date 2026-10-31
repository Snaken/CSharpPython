d = {'a': 1}
def f(**k):
    k['a'] = 99
    return k
f(**d)
print(d)
