# Sharp-Tree

Sharp-Tree is a high-performance, cross-platform command-line utility
for displaying directory structures in a tree-like format.

It is designed to challenge Windows `tree` command.

## Features

- Fast and efficient listing
- Option to include or exclude files in the output
- Adjustable maximum depth for directory traversal
- Cross-platform compatibility

## Installation

Download the latest release from the
[Releases](https://github.com/salty-flower/SharpTree/releases/).
Two types of releases are available:

- NativeAOT: built with `dotnet` one,
  see [Offcial Document](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/). Only support amd64 platform.
- Bflat: built with [`bflat`](https://github.com/bflattened/bflat).
  Support both amd64 and arm64 platform, but no macOS support.

## Usage

To get started, read the built-in help documentation:

```bash
./SharpTree -h
```

## Performance

SharpTree is designed to challenge both the Windows `tree` command
while using a high-level programming language.
It also outperforms the `tree` program installed from `apt`
on my Ubuntu 24.04 WSL2. :)

This is achieved through:

1. Efficient directory traversal using asynchronous operations
2. Optimized memory usage with manual capacity upgrades for string builders
3. Buffered output for improved I/O performance
4. Customizable flush intervals for balancing responsiveness and performance

## Contributing

Contributions to SharpTree are welcome!
Please feel free to submit pull requests, create issues, or suggest improvements.

## License

MIT
