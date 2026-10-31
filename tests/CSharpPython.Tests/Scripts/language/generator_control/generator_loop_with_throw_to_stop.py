class Stop(Exception): pass
def worker():
    n = 0
    try:
        while True:
            try:
                cmd = yield n
            except Stop:
                print('stopping at', n)
                return n
            n += 1
    finally:
        print('worker cleanup')
w = worker()
next(w)
w.send(None); w.send(None)
try:
    w.throw(Stop())
except StopIteration as e:
    print('value', e.value)
