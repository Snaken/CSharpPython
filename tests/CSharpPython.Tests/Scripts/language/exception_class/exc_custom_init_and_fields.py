class HitError(Exception):
    def __init__(self, target, damage):
        super().__init__('hit ' + target)
        self.target = target
        self.damage = damage
try:
    raise HitError('orc', 7)
except HitError as e:
    print(e, e.target, e.damage, e.args)
