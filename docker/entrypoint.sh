#!/bin/sh
set -e

# New files and folders under /data are private to the guardou user.
umask 077

if [ "$(id -u)" = "0" ]; then
    PUID="${PUID:-1000}"
    PGID="${PGID:-1000}"
    case "$PUID$PGID" in
        *[!0-9]*)
            echo "guardou: PUID and PGID must be numeric (got PUID=$PUID, PGID=$PGID)." >&2
            exit 1
            ;;
    esac
    if [ "$PUID" -eq 0 ] || [ "$PGID" -eq 0 ]; then
        echo "guardou: PUID and PGID must not be 0, because the app would run as root." >&2
        echo "guardou: set them to the owner of the data directory, 1000 by default." >&2
        exit 1
    fi
    groupmod -o -g "$PGID" guardou
    usermod -o -u "$PUID" guardou
    find /data /home/guardou \( \! -user "$PUID" -o \! -group "$PGID" \) -exec chown -h "$PUID:$PGID" {} +
fi

exec guardou "$@"
