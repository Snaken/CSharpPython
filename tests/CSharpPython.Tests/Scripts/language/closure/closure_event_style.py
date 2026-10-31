def f():
    log = []
    def on(e):
        log.append(e * 2)
    for e in [1,2,3]:
        on(e)
    return log
print(f())
