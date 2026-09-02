#!/usr/bin/env bash
set -Eeuo pipefail

svix_database="${POSTGRES_SVIX_DB:?缺少 Svix 独立数据库名}"
keycloak_database="${POSTGRES_KEYCLOAK_DB:?缺少 Keycloak 独立数据库名}"

if [[ ! "$svix_database" =~ ^[a-zA-Z_][a-zA-Z0-9_]*$ ]]; then
  echo "Svix 数据库名只能包含字母、数字和下划线" >&2
  exit 1
fi

if [[ "$svix_database" == "$POSTGRES_DB" || "$svix_database" == "$keycloak_database" ]]; then
  echo "Svix 数据库必须与业务数据库和 Keycloak 数据库分离" >&2
  exit 1
fi

database_exists="$(psql --username="$POSTGRES_USER" --dbname="$POSTGRES_DB" --no-password --tuples-only --no-align \
  --command="SELECT 1 FROM pg_database WHERE datname = '$svix_database'")"

if [[ "$database_exists" != "1" ]]; then
  psql --username="$POSTGRES_USER" --dbname="$POSTGRES_DB" --no-password \
    --command="CREATE DATABASE \"$svix_database\""
fi
