try:
    [][1]
except Exception as e:
    print(isinstance(e, (KeyError, IndexError)), isinstance(e, (ValueError, TypeError)), isinstance(e, LookupError))
