def f():
    for i in range(3):
        try:
            yield i
            if i == 1:
                raise ValueError('v')
        except ValueError as e:
            print('handled', e)
        finally:
            print('f', i)
print(list(f()))
def nested():
    try:
        try:
            yield 'a'
            raise KeyError('k')
        except ValueError:
            print('wrong')
        yield 'unreached'
    except KeyError:
        yield 'outer'
print(list(nested()))
