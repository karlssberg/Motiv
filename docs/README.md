# Motiv Documentation

This directory contains the documentation for the Motiv library.

## Building the Documentation

The documentation is built using [DocFX](https://dotnet.github.io/docfx/). To build the documentation locally:

1. Install DocFX: `dotnet tool install -g docfx`
2. Run `docfx build docfx.json` from the root directory
3. The documentation will be generated in the `_site` directory

## Documentation Structure

- `api/`: API documentation generated from code comments
- `articles/`: Conceptual documentation and tutorials
- `toc.yml`: Table of contents for the documentation
- `index.md`: Landing page for the documentation

## Contributing to Documentation

If you'd like to contribute to the documentation, please see the [CONTRIBUTING.md](../CONTRIBUTING.md) file for more information.
