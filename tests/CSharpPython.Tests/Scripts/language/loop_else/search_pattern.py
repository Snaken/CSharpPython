def find(xs, t):
    for i, x in enumerate(xs):
        if x == t:
            return i
    else:
        return -1
print(find([4, 5, 6], 5), find([4, 5, 6], 9))
def primes(n):
    out = []
    for k in range(2, n):
        for d in range(2, k):
            if k % d == 0:
                break
        else:
            out.append(k)
    return out
print(primes(30))
