class NoExit:
    def __enter__(self):
        return self
class NoEnter:
    def __exit__(self, *a):
        pass
for obj in (5, 'text', None, NoExit(), NoEnter(), [1]):
    try:
        with obj:
            print('entered')
    except (TypeError, AttributeError) as e:
        print(type(e).__name__, e)
