| Script | CPython 3.14.7 | .NET 10 (JIT) | Unity 7 IL2CPP | Unity 6.3 IL2CPP | Unity 6.3 Mono |
| --- | ---: | ---: | ---: | ---: | ---: |
| builtins_stream | 61.7 ms | 62.5 ms (1.0x) | 112.0 ms (1.8x) | 113.1 ms (1.8x) | 347.9 ms (5.6x) |
| classes | 5.7 ms | 10.8 ms (1.9x) | 19.5 ms (3.4x) | 24.5 ms (4.3x) | 59.6 ms (10.5x) |
| collections_ops | 10.5 ms | 23.6 ms (2.3x) | 28.7 ms (2.7x) | 33.6 ms (3.2x) | 75.9 ms (7.2x) |
| dict_ops | 3.0 ms | 5.3 ms (1.8x) | 8.7 ms (2.9x) | 8.8 ms (3.0x) | 26.0 ms (8.8x) |
| fib | 1.8 ms | 4.1 ms (2.3x) | 5.4 ms (3.0x) | 5.6 ms (3.1x) | 14.8 ms (8.2x) |
| generators | 3.3 ms | 9.2 ms (2.8x) | 14.1 ms (4.3x) | 14.8 ms (4.5x) | 43.4 ms (13.3x) |
| list_ops | 4.9 ms | 9.5 ms (1.9x) | 14.4 ms (2.9x) | 14.8 ms (3.0x) | 42.1 ms (8.6x) |
| loop_sum | 16.4 ms | 26.7 ms (1.6x) | 43.3 ms (2.6x) | 46.9 ms (2.9x) | 164.8 ms (10.0x) |
| random_math | 11.4 ms | 9.7 ms (0.9x) | 15.1 ms (1.3x) | 18.1 ms (1.6x) | 57.9 ms (5.1x) |
| strings | 1.9 ms | 2.0 ms (1.1x) | 4.7 ms (2.4x) | 6.9 ms (3.5x) | 12.8 ms (6.6x) |
