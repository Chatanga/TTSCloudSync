#!/usr/bin/env bash
SCRIPT_DIR=$(cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd)
SCRIPT_NAME=$(basename "$0" '.sh')
"$SCRIPT_DIR/TTSCloudSync" "$SCRIPT_NAME" "$@"
