for msg in ('text', 42, [1, 2], None, (1, 2), {'a': 1}, 3.5):
    try:
        assert False, msg
    except AssertionError as e:
        print(str(e))
