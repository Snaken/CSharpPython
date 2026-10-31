class E(Exception):
    pass
err = E('saved')
try:
    raise err
except E as e:
    print(e is err)
