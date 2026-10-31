d = {'a': 3, 'b': 1, 'c': 2}
def val(item):
    return item[1]
print(sorted(d.items(), key=val))
