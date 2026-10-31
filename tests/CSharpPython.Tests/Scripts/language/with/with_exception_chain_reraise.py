class CM:
    def __enter__(self):
        return self
    def __exit__(self, t, v, tb):
        return t is KeyError
def run(exc):
    try:
        with CM():
            raise exc
    except Exception as e:
        return type(e).__name__
print(run(KeyError('a')), run(ValueError('b')))
