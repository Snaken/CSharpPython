class E(Exception):
    pass
e = E('x')
e.extra = 5
print(e.extra, e.args)
