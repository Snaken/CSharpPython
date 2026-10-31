class CM:
    def __enter__(self):
        print('enter')
        return 'value'
    def __exit__(self, t, v, tb):
        print('exit', t, v, tb)
        return False
with CM() as x:
    print('body', x)
print('after')
with CM():
    print('no target')
