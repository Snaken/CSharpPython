class Err(Exception):
    pass
class CM:
    def __enter__(self):
        return self
    def __exit__(self, t, v, tb):
        print(t is Err, isinstance(v, Err), v.args)
        return True
with CM():
    raise Err('a', 2)
