# Contributing to Tab Bridge

Tab Bridge is currently a solo project by [@relexx](https://github.com/relexx). Contributions are welcome but will be reviewed on a best-effort basis.

## Reporting bugs

Please use the [bug report template](.github/ISSUE_TEMPLATE/bug_report.md) and include:

- Your browser and version (e.g., Waterfox 6.6.10)
- Your Windows version
- Output of `tab-bridge.exe --status`
- Steps to reproduce

## Feature requests

Please use the [feature request template](.github/ISSUE_TEMPLATE/feature_request.md).

## Pull requests

1. Fork the repo and create a branch from `main`
2. If you add code, add tests
3. Ensure `dotnet test` passes
4. Keep commits focused and well-described
5. Open a PR with a clear description of the change

## Development setup

See the [Architecture document](docs/ARCHITECTURE.md) for the solution structure.

```powershell
# Clone
git clone https://github.com/relexx/tb-tab-bridge.git
cd tb-tab-bridge

# Build
dotnet build src/TabBridge.sln

# Test
dotnet test src/TabBridge.sln

# Publish self-contained
dotnet publish src/TabBridge.Host -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=true
```

## Code style

- C#: follow .NET conventions, use file-scoped namespaces, records for DTOs
- JavaScript: ES modules, no bundler for PoC
- All code in English, comments in English

## License

By contributing, you agree that your contributions will be licensed under the [MPL 2.0](LICENSE).
