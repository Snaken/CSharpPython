class E(KeyError):
    pass
e = E('k')
print(isinstance(e, E), isinstance(e, KeyError), isinstance(e, LookupError), isinstance(e, Exception), isinstance(e, ValueError), isinstance(e, (ValueError, E)))
print(isinstance(1, E), type(e) is E, E.__name__)
