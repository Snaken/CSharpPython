class E(Exception):
    def __str__(self):
        return '<' + super().__str__() + '>'
    def __repr__(self):
        return 'R' + super().__repr__()
e = E('m')
print(str(e), repr(e))
