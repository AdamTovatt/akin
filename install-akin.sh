#!/usr/bin/env bash
set -euo pipefail

REPO="AdamTovatt/akin"
BINARY_NAME="akin"
# Name of the executable inside the release zip. Keep in sync with the CLI
# project name.
BINARY_IN_ZIP="Akin.Cli"
INSTALL_DIR="/usr/local/bin"

check_dependencies() {
    if ! command -v curl &>/dev/null; then
        echo "Error: curl is required." >&2
        exit 1
    fi

    if ! command -v unzip &>/dev/null; then
        echo "Error: unzip is required." >&2
        exit 1
    fi

    if ! command -v sha256sum &>/dev/null && ! command -v shasum &>/dev/null; then
        echo "Error: neither sha256sum nor shasum is installed." >&2
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
    curl -fsSL "https://api.github.com/repos/${REPO}/releases/latest" \
        | grep -o '"tag_name": *"[^"]*"' \
        | head -1 \
        | cut -d'"' -f4 \
        | sed 's/^v//'
}

get_installed_version() {
    if command -v "$BINARY_NAME" &>/dev/null; then
        "$BINARY_NAME" --version 2>/dev/null || echo ""
    else
        echo ""
    fi
}

download() {
    local url="$1"
    local dest="$2"
    if ! curl -fsSL -o "$dest" "$url"; then
        echo "Failed to download $url" >&2
        return 1
    fi
}

main() {
    check_dependencies

    # Clean up any prior install that used the old /usr/local/lib/akin layout.
    if [ -d /usr/local/lib/akin ]; then
        if [ -w /usr/local/lib ]; then
            rm -rf /usr/local/lib/akin
        else
            sudo rm -rf /usr/local/lib/akin
        fi
    fi

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

    local base_url="https://github.com/${REPO}/releases/download/v${version}"

    echo "Downloading akin v${version}..."
    download "${base_url}/${asset}" "${TMP_DIR}/${asset}"
    download "${base_url}/${asset}.sha256" "${TMP_DIR}/${asset}.sha256"

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
    # Remove any existing symlink from older installs before placing the
    # real file.
    if [ -L "${INSTALL_DIR}/${BINARY_NAME}" ]; then
        if [ -w "$INSTALL_DIR" ]; then
            rm -f "${INSTALL_DIR}/${BINARY_NAME}"
        else
            sudo rm -f "${INSTALL_DIR}/${BINARY_NAME}"
        fi
    fi

    if [ -w "$INSTALL_DIR" ]; then
        mv "$binary_path" "${INSTALL_DIR}/${BINARY_NAME}"
    else
        sudo mv "$binary_path" "${INSTALL_DIR}/${BINARY_NAME}"
    fi

    echo "Installed akin $version successfully."
}

main
