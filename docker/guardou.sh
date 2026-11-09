#!/bin/sh
# docker exec starts a new process, so it does not inherit the entrypoint's umask.
umask 077
if [ "$(id -u)" = "0" ]; then
    exec su-exec guardou dotnet /app/guardou.dll "$@"
fi
exec dotnet /app/guardou.dll "$@"
