# Contributing to Motiv

Thank you for your interest in contributing to Motiv. We appreciate your effort to help make Motiv a valuable tool for working with boolean logic in .NET applications.

## Code of Conduct

This project and everyone participating in it is governed by the [Motiv Code of Conduct](CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code. 

For reporting Code of Conduct violations or other sensitive issues, please use one of the following methods:
- Open a private security advisory in the GitHub repository
- Contact the project maintainers via GitHub's contact feature
- For general questions or discussions, please use GitHub Discussions in this repository

## Getting Started

### Prerequisites

- Ensure you have the [.NET SDK](https://dotnet.microsoft.com/download) installed on your machine.
- Familiarity with Git and GitHub.

### Setting up the development environment

1. Clone the repository:
   ```
   git clone https://github.com/karlssberg/Motiv.git
   ```
2. Navigate to the project directory:
   ```
   cd Motiv
   ```

### Building and Testing

To build the project:
```
dotnet build
```

To run tests:
```
dotnet test
```

## How to Contribute

### Branching Strategy

We use GitHub Flow, a simple and effective branching strategy:

- `main`: The primary branch. It should always be stable and deployable.
- `feature/`: Used for developing new features or fixes. Create directly from `main` and merge back into `main`.

### Contribution Process

1. Create a new feature branch from `main`:
   ```
   git checkout main
   git pull origin main
   git checkout -b feature/your-feature-name
   ```
2. Make your changes in your feature branch.
3. Keep your feature branch up to date with `main`:
   ```
   git checkout main
   git pull origin main
   git checkout feature/your-feature-name
   git merge main
   ```
4. Ensure your changes adhere to the coding standards used throughout the project.
5. Update the README.md with details of changes to the interface, if applicable.
6. Commit your changes:
   ```
   git commit -m "A brief description of your changes"
   ```
7. Push your branch to GitHub:
   ```
   git push origin feature/your-feature-name
   ```
8. Create a Pull Request on GitHub to merge your changes into `main`.

### Pull Request Process

1. Ensure any install or build dependencies are removed before the end of the layer when doing a build.
2. Update the README.md with details of changes to the interface, this includes new environment variables, exposed ports, useful file locations and container parameters.
3. Increase the version numbers in any examples files and the README.md to the new version that this Pull Request would represent. The versioning scheme we use is [SemVer](http://semver.org/).
4. You may merge the Pull Request once you have the sign-off of two other developers, or if you do not have permission to do that, you may request the second reviewer to merge it for you.

## Reporting Issues

- Use the issue tracker to report bugs.
- Use the issue tracker to suggest feature requests and enhancements.
- For bug reports, please provide:
  - A quick summary and/or background
  - Steps to reproduce
  - What you expected would happen
  - What actually happens
  - Notes (possibly including why you think this might be happening, or stuff you tried that didn't work)

## Style Guidelines

- Follow the [C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/inside-a-program/coding-conventions) from Microsoft.
- Use clear, descriptive names for methods and variables.
- Write comments for complex logic or non-obvious code sections.
- Keep methods small and focused on a single task.

## Review Process

- All submissions, including submissions by project members, require review.
- We aim to review pull requests within 7 days.
- After feedback has been given, we expect responses within two weeks. After two weeks, we may close the pull request if it isn't showing any activity.

## Community

- If you have questions or need help, please open an issue in the GitHub issue tracker.
- For general discussions about development, use the Discussions feature on GitHub.

## Acknowledgements

Contributors will be acknowledged in the project's README.md file. We appreciate all forms of contribution, from code to documentation to design.

Thank you for contributing to Motiv!
