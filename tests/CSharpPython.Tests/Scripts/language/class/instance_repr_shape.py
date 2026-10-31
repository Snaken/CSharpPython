class A: pass
r = repr(A())
print(r.startswith('<'), 'A object at' in r, r.endswith('>'))
