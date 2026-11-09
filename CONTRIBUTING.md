# Contributing

Thanks for wanting to help make this fork of Guardou better!

This repository is a fork of [GuardouApp/Guardou](https://github.com/GuardouApp/Guardou), and the rules below are this fork's own.

## Before you start

Before you work on a new feature, open an issue to agree on the approach. Bug fixes and security fixes can go straight to a pull request.

## How to contribute

- Report bugs you find in this fork's [issues](https://github.com/albrtbc/Guardou/issues).
- Improve the documentation. It sits under the `docs` folder and is powered by [Docusaurus](https://docusaurus.io/).
- Fix open issues, or implement agreed features, through pull requests.

## AI Usage Policy

AI-assisted contributions are welcome, including changes written mostly by AI tools. What counts is the quality of the result, not how it was produced:

* You own the change. Whoever opens the pull request is responsible for every line, and must be able to explain what it does and why.
* Tests are required. Behaviour changes come with tests, and the existing suites must pass: `dotnet test` for the server, and `flutter analyze` plus `flutter test` for the frontend.
* The build must pass with no warnings, including a committed, up-to-date `api/openapi.json`. The fork runs no CI on pull requests, so run the checks locally.
* Security-sensitive changes (authentication, sharing, uploads, file serving) get a review focused on them before they are merged.
* Say briefly in the pull request description how AI was used, for example "implemented with an AI agent, reviewed and tested by me".
* Documentation, UI text and other user-facing text may be AI-written too, but a human reviews them before they are merged.
