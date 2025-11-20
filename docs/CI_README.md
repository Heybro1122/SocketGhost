# CI/CD Documentation

## Overview

SocketGhost uses GitHub Actions for continuous integration and deployment. This document explains how the workflows operate and how to reproduce builds locally.

## Workflows

### 1. Build Core (`build-core.yml`)

**Triggers**:
- Push to `main` or `release/**` branches
- Pull requests to `main`
- Changes to `socketghost-core/**`

**Steps**:
1. Checkout code
2. Setup .NET 7.0 SDK
3. Cache NuGet packages
4. Restore dependencies
5. Build (Release configuration)
6. Run tests (if any)
7. Publish to `./publish`
8. Package as ZIP
9. Upload artifact (30-day retention)

**Reproduce Locally**:
```bash
# Install .NET 7.0 SDK first
dotnet restore socketghost-core/socketghost-core.csproj
dotnet build socketghost-core/socketghost-core.csproj -c Release
dotnet publish socketghost-core/socketghost-core.csproj -c Release -o ./publish
```

---

### 2. Build UI (`build-ui.yml`)

**Triggers**:
- Push to `main` or `release/**` branches
- Pull requests to `main`
- Changes to `socketghost-ui/**`

**Jobs**:

#### build-web (Ubuntu)
- Builds web bundle (Vite dist/)
- Packages as ZIP
- Runs on every trigger

#### build-native-windows (Windows)
- Installs Rust toolchain
- Builds Tauri MSI installer
- Runs on every trigger

**Reproduce Locally**:

**Web Bundle**:
```bash
cd socketghost-ui
npm ci
npm run build
# Output: dist/
```

**Native Windows Installer**:
```powershell
# Prerequisites:
# - Node.js 18+
# - Rust (rustup-init.exe)
# - MSVC Build Tools

cd socketghost-ui
npm ci
npm run build
npm run tauri build
# Output: src-tauri/target/release/bundle/msi/*.msi
```

---

### 3. Docker Publish (`docker-publish.yml`)

**Triggers**:
- Push to `main` branch
- Tags matching `v*.*.*` pattern
- Manual dispatch

**Steps**:
1. Checkout code
2. Login to GitHub Container Registry (GHCR)
3. Extract Docker metadata (tags, labels)
4. Build multi-stage image
5. Push to `ghcr.io/YOUR_ORG/socketghost-core`
6. Cache layers for faster rebuilds

**Tags Generated**:
- `latest` (on main branch)
- `v1.0.0` (on tag v1.0.0)
- `v1.0` (major.minor on tag)
- `v1` (major on tag)

**Reproduce Locally**:
```bash
# Build image
docker build -t socketghost-core:latest -f socketghost-core/Dockerfile .

# Run locally
docker run -p 8080:8080 -p 9000:9000 -p 9100:9100 -p 9200:9200 -p 9300:9300 socketghost-core:latest

# Push to GHCR (requires authentication)
echo $GITHUB_TOKEN | docker login ghcr.io -u YOUR_USERNAME --password-stdin
docker tag socketghost-core:latest ghcr.io/YOUR_ORG/socketghost-core:v1.0.0
docker push ghcr.io/YOUR_ ORG/socketghost-core:v1.0.0
```

---

### 4. Release (`release.yml`)

**Triggers**:
- Push tags matching `v*.*.*` (e.g., `v1.0.0`)

**Steps**:
1. **create-release** job:
   - Creates GitHub draft release
   - Uses `RELEASE_DRAFT.md` as body

2. **build-artifacts** job (matrix strategy):
   - Ubuntu: Builds core ZIP
   - Ubuntu: Builds UI web ZIP
   - Windows: Builds UI native MSI
   - Generates SHA256 checksums
   - Uploads all artifacts to release

**Reproduce Locally**:
```bash
# Use packaging scripts
chmod +x scripts/package_core.sh scripts/package_ui.sh
./scripts/package_core.sh
./scripts/package_ui.sh

# Generate checksums
./scripts/create_release_assets.sh

# Artifacts in ./artifacts/
```

**Manual Release**:
```bash
# Tag and push
git tag -a v1.0.0 -m "Release v1.0.0"
git push origin v1.0.0

# GitHub Actions will automatically:
# - Build artifacts
# - Create draft release
# - Upload assets

# Then manually publish the draft release in GitHub UI
```

---

### 5. CodeQL Analysis (`codeql-analysis.yml`) [Optional]

**Triggers**:
- Push to `main`
- Pull requests
- Schedule (weekly on Monday 06:00 UTC)

**Languages Scanned**:
- C# (socketghost-core)
- JavaScript/TypeScript (socketghost-ui)

**Reproduce Locally**:
```bash
# Install CodeQL CLI
# https://github.com/github/codeql-cli-binaries

# Create database
codeql database create codeql-db --language=csharp --source-root=socketghost-core

# Run queries
codeql database analyze codeql-db csharp-security-and-quality.qls --format=sarif-latest --output=results.sarif
```

---

## Secrets Required

Add these to GitHub repository settings → Secrets and variables → Actions:

| Secret | Purpose | Required By |
|--------|---------|-------------|
| `GITHUB_TOKEN` | Automatic (provided by GitHub) | All workflows |
| `GHCR_TOKEN` | Push Docker images (or use GITHUB_TOKEN) | docker-publish.yml |

**Note**: For GHCR, `GITHUB_TOKEN` with `packages:write` permission is sufficient (default).

---

## Artifact Retention

- **Build artifacts**: 30 days
- **Release artifacts**: Permanent (attached to GitHub Release)

To download artifacts:
```bash
# Install GitHub CLI
gh run download RUN_ID --name socketghost-core-COMMIT_SHA
```

---

## Caching Strategy

### NuGet Cache (Core builds)
```yaml
~/.nuget/packages
# Key: OS-nuget-hash(*.csproj)
```

### npm Cache (UI builds)
```yaml
~/.npm
# Managed by actions/setup-node with cache: 'npm'
```

### Docker Layer Cache
```yaml
type=gha  # GitHub Actions cache
# Automatically managed by docker/build-push-action
```

---

## Troubleshooting

### Build Fails Locally But Passes in CI
- **Cause**: Different Node.js/npm versions
- **Fix**: Match versions in `build-ui.yml` (Node 18+)

### Docker Build Timeouts
- **Cause**: Large layer downloads
- **Fix**: Use cache: `--cache-from=type=gha`

### MSI Build Fails on Windows
- **Cause**: Missing Rust or MSVC
- **Fix**:
  ```powershell
  # Install Visual Studio Build Tools
  winget install Microsoft.VisualStudio.2022.BuildTools
  
  # Install Rust
  winget install Rustlang.Rustup
  ```

### Release Workflow Not Triggering
- **Cause**: Tag pushed before workflow file existed
- **Fix**: Delete and re-create tag:
  ```bash
  git tag -d v1.0.0
  git push origin :refs/tags/v1.0.0
  git tag -a v1.0.0 -m "Release v1.0.0"
  git push origin v1.0.0
  ```

---

## Self-Hosted Runners

For faster builds or native macOS installers, use self-hosted runners:

**.github/workflows/build-ui.yml** (macOS example):
```yaml
jobs:
  build-native-macos:
    runs-on: [self-hosted, macos]
    steps:
      - uses: actions/checkout@v3
      - name: Build DMG
        run: |
          cd socketghost-ui
          npm ci
          npm run tauri build
```

**Setup**:
1. Navigate to Repo Settings → Actions → Runners
2. Click "New self-hosted runner"
3. Follow setup instructions
4. Label runner (e.g., `macos`, `windows`, `linux`)

---

## Performance Tips

1. **Parallel Jobs**: Matrix strategy runs builds concurrently
2. **Conditional Workflows**: Use `paths` filter to skip unnecessary builds
3. **Cache Everything**: NuGet, npm, Docker layers
4. **Artifact Compression**: Use ZIP over tar.gz (faster on Windows)
5. **Incremental Builds**: Reuse artifacts across jobs

---

## Monitoring

**View Workflow Runs**:
```bash
gh run list --workflow=build-core.yml --limit=10
gh run view RUN_ID --log
```

**Status Badge** (Add to README):
```markdown
![Build Core](https://github.com/YOUR_ORG/SocketGhost/actions/workflows/build-core.yml/badge.svg)
```

---

## References

- [GitHub Actions Docs](https://docs.github.com/en/actions)
- [Caching Dependencies](https://docs.github.com/en/actions/using-workflows/caching-dependencies-to-speed-up-workflows)
- [Publishing Docker Images](https://docs.github.com/en/actions/publishing-packages/publishing-docker-images)
- [Creating Releases](https://docs.github.com/en/repositories/releasing-projects-on-github/automatically-generated-release-notes)
