try:
    1 // 0
except Exception as e:
    print(isinstance(e, ZeroDivisionError), isinstance(e, ArithmeticError), isinstance(e, Exception), isinstance(e, ValueError))
