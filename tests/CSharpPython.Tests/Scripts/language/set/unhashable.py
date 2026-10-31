for bad in ([1], {1: 2}, {1}):
    try:
        {bad}
    except TypeError:
        print('TypeError')
    try:
        {1}.add(bad)
    except TypeError:
        print('TypeError')
    try:
        {bad: 1}
    except TypeError as e:
        print('TypeError', type(e).__name__)
    try:
        hash(bad)
    except TypeError as e:
        print('TypeError', e)
    try:
        bad in {1}
    except TypeError:
        print('TypeError')
