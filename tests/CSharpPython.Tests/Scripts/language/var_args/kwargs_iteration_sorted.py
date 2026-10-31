def f(**k):
    out = []
    for key in sorted(k):
        out.append(key + '=' + str(k[key]))
    return ', '.join(out)
print(f(b=2, a=1))
