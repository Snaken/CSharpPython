class E(Exception):
    pass
print(repr(E()), repr(E('a')), repr(E(1, 'b')), str(E()), str(E('a')), str(E(1, 'b')))
print(E('q').args, E().args)
