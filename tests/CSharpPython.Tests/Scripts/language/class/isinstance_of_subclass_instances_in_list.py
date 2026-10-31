class Animal: pass
class Dog(Animal): pass
class Cat(Animal): pass
zoo = [Dog(), Cat(), Dog(), 5]
print(sum(1 for z in zoo if isinstance(z, Dog)), sum(1 for z in zoo if isinstance(z, Animal)))
