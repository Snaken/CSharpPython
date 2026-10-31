def inner(t):
    return sorted(t)[0]
print(sorted([[3,1],[2,5],[0,9]], key=inner))
