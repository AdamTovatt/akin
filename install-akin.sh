#!/usr/bin/env bash
set -euo pipefail

REPO="AdamTovatt/akin"
BINARY_NAME="akin"
# The self-contained single-file publish produces an executable named after
# the CLI project. Keep this in sync with the project name if it's ever renamed.
BINARY_IN_ZIP="Akin.Cli"
INSTALL_DIR="/usr/local/bin"

check_dependencies() {
    if ! command -v gh &>/dev/null; then
        echo "Error: GitHub CLI (gh) is required. Install it from https://cli.github.com/" >&2
        exit 1
    fi

    if ! gh auth status &>/dev/null; then
        echo "Error: Not authenticated with GitHub CLI. Run 'gh auth login' first." >&2
        exit 1
    fi

    if ! command -v sha256sum &>/dev/null && ! command -v shasum &>/dev/null; then
        echo "Error: Neither sha256sum nor shasum is installed." >&2
        exit 1
    fi
}

verify_sha256() {
    local file="$1"
    local expected="$2"

    local actual
    if command -v sha256sum &>/dev/null; then
        actual="$(sha256sum "$file" | awk '{print $1}')"
    else
        actual="$(shasum -a 256 "$file" | awk '{print $1}')"
    fi

    if [ "$actual" != "$expected" ]; then
        echo "Checksum verification failed for $file." >&2
        echo "  Expected: $expected" >&2
        echo "  Actual:   $actual" >&2
        return 1
    fi
}

detect_asset() {
    local os arch
    os="$(uname -s)"
    arch="$(uname -m)"

    case "$os" in
        Darwin) os="osx" ;;
        Linux)  os="linux" ;;
        *)      echo "Unsupported OS: $os" >&2; exit 1 ;;
    esac

    case "$arch" in
        x86_64|amd64)  arch="x64" ;;
        arm64|aarch64) arch="arm64" ;;
        *)             echo "Unsupported architecture: $arch" >&2; exit 1 ;;
    esac

    echo "akin-${os}-${arch}.zip"
}

get_latest_version() {
    # /releases/latest excludes prereleases and drafts by default.
    gh api "repos/${REPO}/releases/latest" --jq '.tag_name' | sed 's/^v//'
}

get_installed_version() {
    if command -v "$BINARY_NAME" &>/dev/null; then
        "$BINARY_NAME" --version 2>/dev/null || echo ""
    else
        echo ""
    fi
}

main() {
    check_dependencies

    echo "Detecting platform..."
    local asset
    asset="$(detect_asset)"
    echo "  Asset: $asset"

    echo "Fetching latest version..."
    local version
    version="$(get_latest_version)"
    if [ -z "$version" ]; then
        echo "Failed to determine latest version." >&2
        exit 1
    fi
    echo "  Latest: $version"

    local installed
    installed="$(get_installed_version)"
    if [ -n "$installed" ] && [ "$installed" = "$version" ]; then
        echo "Already up to date ($version)."
        exit 0
    fi

    if [ -n "$installed" ]; then
        echo "  Installed: $installed — upgrading..."
    else
        echo "  No existing installation found — installing..."
    fi

    TMP_DIR="$(mktemp -d)"
    trap 'rm -rf "$TMP_DIR"' EXIT

    echo "Downloading akin v${version} and its checksum..."
    gh release download "v${version}" -R "$REPO" -p "$asset" -p "${asset}.sha256" -D "$TMP_DIR"

    echo "Verifying checksum..."
    local expected_sum
    expected_sum="$(awk '{print $1}' "${TMP_DIR}/${asset}.sha256")"
    verify_sha256 "${TMP_DIR}/${asset}" "$expected_sum"
    echo "  OK"

    echo "Extracting..."
    unzip -qo "${TMP_DIR}/${asset}" -d "${TMP_DIR}/extracted"

    local binary_path="${TMP_DIR}/extracted/${BINARY_IN_ZIP}"
    if [ ! -f "$binary_path" ]; then
        echo "Binary '${BINARY_IN_ZIP}' not found in archive." >&2
        exit 1
    fi

    chmod +x "$binary_path"

    echo "Installing to ${INSTALL_DIR}/${BINARY_NAME}..."
    if [ -w "$INSTALL_DIR" ]; then
        mv "$binary_path" "${INSTALL_DIR}/${BINARY_NAME}"
    else
        sudo mv "$binary_path" "${INSTALL_DIR}/${BINARY_NAME}"
    fi

    echo "Installed akin $version successfully."
}

main
