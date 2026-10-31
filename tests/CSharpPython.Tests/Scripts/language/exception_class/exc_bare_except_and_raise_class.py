class E(Exception):
    pass
try:
    raise E
except:
    print('bare')
