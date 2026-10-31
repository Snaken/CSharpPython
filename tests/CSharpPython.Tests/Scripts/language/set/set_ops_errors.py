def attempt(fn):
    try:
        fn()
    except TypeError as e:
        print('TypeError', e)
attempt(lambda: {1} - [1])
attempt(lambda: [1] - {1})
attempt(lambda: {1} + {2})
attempt(lambda: {1} < [1])
attempt(lambda: {1} < 2)
attempt(lambda: {1} * 2)
try:
    {1}.add()
except TypeError as e:
    print('TypeError')
try:
    {1}.symmetric_difference()
except TypeError as e:
    print('TypeError')
try:
    {1}.union(5)
except TypeError as e:
    print('TypeError', e)
try:
    {1}.nothing
except AttributeError as e:
    print('AttributeError', e)
