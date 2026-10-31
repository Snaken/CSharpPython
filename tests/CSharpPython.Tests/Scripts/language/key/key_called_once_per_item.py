calls = []
def k(x):
    calls.append(x)
    return x
sorted([3,1,2], key=k)
print(calls)
