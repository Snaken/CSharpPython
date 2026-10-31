def f(a, b=0):
    return a + b
print(f(t := 4), t)
print(f(1, b=(u := 9)), u)
xs = [10, 20, 30]
print(xs[(j := 1)], j, xs[j := 2], j)
print(len(r := [1, 2, 3]), r)
