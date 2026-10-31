log = []
class CM:
    def __enter__(self):
        log.append('e')
    def __exit__(self, *a):
        log.append('x')
for i in range(2):
    try:
        with CM():
            if i == 0:
                raise ValueError('v')
            log.append('body')
    except ValueError:
        log.append('caught')
    finally:
        log.append('f')
print(log)
