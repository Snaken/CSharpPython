class E(Exception):
    pass
try:
    raise E('a', 'b')
except E as e:
    print(f'{e}', f'{e!r}')
