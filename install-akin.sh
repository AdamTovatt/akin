#!/usr/bin/env bash
set -euo pipefail

REPO="AdamTovatt/akin"
BINARY_NAME="akin"
# The self-contained single-file publish produces an executable named after
# the CLI project. Keep this in sync with the project name if it's ever renamed.
BINARY_IN_ZIP="Akin.Cli"
# The binary needs its Models/ sidecar directory at runtime, so we install the
# whole extracted layout here and symlink the binary into INSTALL_DIR.
LIB_DIR="/usr/local/lib/akin"
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

privileged_run() {
    if [ -w "$(dirname "$1")" ] 2>/dev/null || [ "$(id -u)" = "0" ]; then
        "$@"
    else
        sudo "$@"
    fi
}

install_files() {
    local staging="$1"

    # Remove any previous install of the lib directory so stale files (old
    # model versions, old native libs) don't linger. Recreate empty.
    if [ -d "$LIB_DIR" ]; then
        if [ -w "$(dirname "$LIB_DIR")" ]; then
            rm -rf "$LIB_DIR"
        else
            sudo rm -rf "$LIB_DIR"
        fi
    fi

    if [ -w "$(dirname "$LIB_DIR")" ]; then
        mkdir -p "$LIB_DIR"
        cp -R "$staging"/. "$LIB_DIR"/
        chmod +x "$LIB_DIR/$BINARY_IN_ZIP"
    else
        sudo mkdir -p "$LIB_DIR"
        sudo cp -R "$staging"/. "$LIB_DIR"/
        sudo chmod +x "$LIB_DIR/$BINARY_IN_ZIP"
    fi

    local link_target="$LIB_DIR/$BINARY_IN_ZIP"
    local link_path="$INSTALL_DIR/$BINARY_NAME"

    # Replace any existing binary or symlink at the install path.
    if [ -w "$INSTALL_DIR" ]; then
        rm -f "$link_path"
        ln -s "$link_target" "$link_path"
    else
        sudo rm -f "$link_path"
        sudo ln -s "$link_target" "$link_path"
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

    if [ ! -f "${TMP_DIR}/extracted/${BINARY_IN_ZIP}" ]; then
        echo "Binary '${BINARY_IN_ZIP}' not found in archive." >&2
        exit 1
    fi

    echo "Installing to ${LIB_DIR} and linking ${INSTALL_DIR}/${BINARY_NAME}..."
    install_files "${TMP_DIR}/extracted"

    echo "Installed akin $version successfully."
}

main
