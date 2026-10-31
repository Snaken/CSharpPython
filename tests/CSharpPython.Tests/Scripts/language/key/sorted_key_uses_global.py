order = {'a': 3, 'b': 1, 'c': 2}
def rank(k):
    return order[k]
print(sorted(['a','b','c'], key=rank))
