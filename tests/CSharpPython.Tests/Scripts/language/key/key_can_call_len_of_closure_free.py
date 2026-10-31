data = {'x': [1,2,3], 'y': [1], 'z': [1,2]}
def size(k):
    return len(data[k])
print(sorted(data, key=size))
