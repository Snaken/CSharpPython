| Script | CPython 3.14.7 | .NET 10 (JIT) | Unity 7 IL2CPP | Unity 6.3 IL2CPP | Unity 6.3 Mono |
| --- | ---: | ---: | ---: | ---: | ---: |
| builtins_stream | 63.8 ms | 64.8 ms (1.0x) | 112.9 ms (1.8x) | 115.5 ms (1.8x) | 354.1 ms (5.6x) |
| classes | 5.9 ms | 11.2 ms (1.9x) | 19.5 ms (3.3x) | 25.2 ms (4.3x) | 58.7 ms (9.9x) |
| collections_ops | 11.0 ms | 24.0 ms (2.2x) | 29.4 ms (2.7x) | 34.8 ms (3.2x) | 75.4 ms (6.8x) |
| dict_ops | 3.1 ms | 5.3 ms (1.7x) | 8.9 ms (2.9x) | 8.9 ms (2.9x) | 26.3 ms (8.5x) |
| fib | 1.9 ms | 3.9 ms (2.0x) | 5.3 ms (2.7x) | 5.4 ms (2.8x) | 13.8 ms (7.2x) |
| generators | 3.3 ms | 8.7 ms (2.6x) | 12.8 ms (3.8x) | 13.8 ms (4.1x) | 36.0 ms (10.8x) |
| list_ops | 5.1 ms | 9.8 ms (1.9x) | 14.6 ms (2.9x) | 15.2 ms (3.0x) | 42.6 ms (8.4x) |
| loop_sum | 17.0 ms | 27.8 ms (1.6x) | 42.7 ms (2.5x) | 47.8 ms (2.8x) | 166.0 ms (9.7x) |
| random_math | 11.8 ms | 10.2 ms (0.9x) | 15.3 ms (1.3x) | 18.8 ms (1.6x) | 57.9 ms (4.9x) |
| strings | 1.9 ms | 2.1 ms (1.1x) | 4.7 ms (2.5x) | 7.0 ms (3.7x) | 13.0 ms (6.8x) |
