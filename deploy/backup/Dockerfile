FROM alpine:3.22
RUN apk add --no-cache gnupg \
    && addgroup -g 1654 secureshare \
    && adduser -D -u 1000 -G secureshare backup
USER backup
ENTRYPOINT ["/bin/sh", "-c"]

