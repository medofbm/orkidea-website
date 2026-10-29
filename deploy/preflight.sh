#!/usr/bin/env bash
# Read-only inventory. Does not read application environment files or credentials.
set -eu
hostname
cat /etc/os-release
nproc
free -h
df -h /
docker version --format '{{.Server.Version}}'
docker compose version
docker ps --format 'table {{.Names}}\t{{.Image}}\t{{.Ports}}\t{{.Status}}'
docker network ls --format '{{.Name}} {{.Driver}}'
ss -ltn
if command -v nginx >/dev/null; then nginx -v; fi
