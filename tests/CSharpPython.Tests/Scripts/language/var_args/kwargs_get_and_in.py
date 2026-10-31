def f(**k):
    return k.get('a', 'none'), 'b' in k, len(k)
print(f(b=1))
