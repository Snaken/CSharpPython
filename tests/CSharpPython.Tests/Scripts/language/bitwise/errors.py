def attempt(fn):
    try:
        print(fn())
    except Exception as e:
        print(type(e).__name__, e)
attempt(lambda: 1 << -1)
attempt(lambda: 1 >> -1)
attempt(lambda: 1.5 & 1)
attempt(lambda: 1 | 2.0)
attempt(lambda: 'a' | 'b')
attempt(lambda: [1] & [1])
attempt(lambda: None ^ 1)
attempt(lambda: ~1.5)
attempt(lambda: ~'a')
attempt(lambda: ~None)
attempt(lambda: 1 << 'a')
attempt(lambda: {1: 2} & {1: 2})
attempt(lambda: (1,) | (2,))
attempt(lambda: 1 >> [])
