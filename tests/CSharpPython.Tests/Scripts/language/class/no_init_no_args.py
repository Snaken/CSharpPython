class V:
    pass
print(type(V()).__name__)
try:
    V(1)
except TypeError:
    print('TypeError')
