FROM golang:1.26-alpine AS build
WORKDIR /src
COPY go.mod go.sum ./
RUN go mod download
COPY . .
RUN CGO_ENABLED=0 go build -trimpath -ldflags="-s -w" -o /out/server ./cmd/server

FROM alpine:3.22
RUN apk add --no-cache git ca-certificates && adduser -D -u 10001 app
USER app
COPY --from=build /out/server /usr/local/bin/server
ENV HTTP_ADDR=:8080 GIT_WORKDIR=/tmp
EXPOSE 8080
ENTRYPOINT ["/usr/local/bin/server"]
