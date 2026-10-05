FROM alpine:3.22
# The short-lived backup job must read volumes owned by both the application
# UID (1654) and SQL Server UID (10001), plus root-owned wrapping secrets.
# Source mounts are read-only and this image is never used as a network service.
RUN apk add --no-cache gnupg
ENTRYPOINT ["/bin/sh", "-c"]

