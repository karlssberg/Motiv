# Motive

Motive is a solution that vastly simplifies the process of understanding the causes of decisions in an applications.  It has many similarities to validation libraries, but its purpose goes beyond merely guarding against invalid state (although it can be used like this). Instead, it provides a framework to provide custom explanations/metadata about why a decision was made, or not.

What's more, since Motive is an implementation of the specification pattern it lends itself as a tool to encapsulate domain logic (or even non-domain logic)- in a way that is reusable and thoroughly testable.

## Overview

- **Target Framework**: .NETStandard, Version=v2.0, net8.0
- **Language**: C# 12.0
- **IDE**: JetBrains Rider 2023.3.1

## Problem Statement

After releasing an application and getting feedback from users it can be challenging trying figure out why unexpected decisions were arrived at.  It can be equally challenging for the developers when trying to understand the logic of the application whilst debugging it if the logic is sufficiently complex or decomposed and scattered across the codebase.

## Solution

Motive addresses these challenges by providing a mechanism akin to validation libraries. However, where traditional validations stop at providing a binary valid or invalid status, Motive goes a step further.

1. **Metadata Accumulation**: Motive requires metadata to be supplied to specification classes. If a specification has significantly impacted the decision of the application, its metadata and any other metadata that influenced the decision are accumulated and relayed to the caller.

2. **Enhanced Debugging Experience**: Each specification overrides the `ToString()` method, resulting in a serialized representation of the boolean logic in action. This makes debugging more insightful.

3. **Clear Communication of Outcomes**: With the final outcome tree, the `ToString()` method reveals not just the comparison of specification objects but also the boolean result, contributing towards a complete understanding of the underlying logic.

Effectively, Motive can replace many validation libraries, offering an enriched validation experience by providing a layer of abstraction that unifies the decision-making process and the validation process.

## Getting Started

1. **Installation**: Ensure you have `.NET SDK 8.0` or above and JetBrains Rider or Visual Studio IDE installed.
2. **Clone the Project**:

    ```
    git clone <URL_TO_GIT_REPO>
    ```

3. **Build the Solution**: Open the solution file (.sln) in JetBrains Rider 2023.3.1 and press `Ctrl+F9`.
4. **Running the Solution**: Press `Ctrl+F5`.

## Contribution

Your contributions to Motive are greatly appreciated:

1. **Branching Strategy**: [Replace with your strategy]
2. **Pull Requests**: [Replace with your guidelines]
3. **Issue Reporting**: [Replace with your process]

## License

[Insert license details here]

## Contact

For more information, feel free to reach out at [Insert contact info here].