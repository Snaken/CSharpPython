print(getattr([1], 'append', None) is not None, getattr(1, 'nope', 'd'))
