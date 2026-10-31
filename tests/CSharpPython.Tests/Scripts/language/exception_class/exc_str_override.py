class E(Exception):
    def __str__(self):
        return 'custom text'
try:
    raise E('ignored')
except E as e:
    print(e, str(e), repr(e), e.args)
