class Plain:
    pass
try:
    raise Plain()
except TypeError as e:
    print(e)
