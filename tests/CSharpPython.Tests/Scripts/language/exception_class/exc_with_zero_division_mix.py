class E(ArithmeticError):
    pass
for f in (lambda: 1 / 0, lambda: (_ for _ in ()).throw(E('g')) if False else (_ for _ in ()).__next__() if False else (1 // 0)):
    try:
        f()
    except E:
        print('E')
    except ArithmeticError as e:
        print('Arith', type(e).__name__)
