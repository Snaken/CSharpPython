| Script | CPython 3.14.7 | .NET 10 (JIT) | Unity 7 IL2CPP | Unity 6.3 IL2CPP | Unity 6.3 Mono |
| --- | ---: | ---: | ---: | ---: | ---: |
| builtins_stream | 62.5 ms | 62.0 ms (1.0x) | 111.9 ms (1.8x) | 114.6 ms (1.8x) | 347.9 ms (5.6x) |
| classes | 5.8 ms | 12.7 ms (2.2x) | 22.4 ms (3.9x) | 29.9 ms (5.1x) | 73.3 ms (12.6x) |
| collections_ops | 10.9 ms | 24.0 ms (2.2x) | 29.3 ms (2.7x) | 34.4 ms (3.2x) | 77.0 ms (7.1x) |
| dict_ops | 3.1 ms | 5.3 ms (1.7x) | 8.8 ms (2.8x) | 8.9 ms (2.8x) | 25.9 ms (8.3x) |
| fib | 1.8 ms | 4.8 ms (2.6x) | 6.2 ms (3.3x) | 7.8 ms (4.2x) | 20.7 ms (11.2x) |
| generators | 3.5 ms | 9.3 ms (2.7x) | 14.1 ms (4.1x) | 15.0 ms (4.3x) | 43.6 ms (12.6x) |
| list_ops | 5.1 ms | 9.6 ms (1.9x) | 14.3 ms (2.8x) | 14.9 ms (2.9x) | 42.5 ms (8.3x) |
| loop_sum | 16.9 ms | 27.1 ms (1.6x) | 43.3 ms (2.6x) | 47.6 ms (2.8x) | 164.5 ms (9.7x) |
| random_math | 11.6 ms | 11.6 ms (1.0x) | 16.2 ms (1.4x) | 19.9 ms (1.7x) | 67.4 ms (5.8x) |
| strings | 2.0 ms | 2.1 ms (1.1x) | 4.9 ms (2.5x) | 6.7 ms (3.4x) | 12.9 ms (6.6x) |
