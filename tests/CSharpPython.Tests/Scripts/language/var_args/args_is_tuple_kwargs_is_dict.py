def f(*a, **k):
    return type_name(a), type_name(k)
def type_name(v):
    return 'tuple' if v == () or isinstance_tuple(v) else 'dict'
def isinstance_tuple(v):
    return False
print(f()[0])
