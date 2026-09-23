#!/bin/sh
set -eu

# Hosted Postgres services such as Railway and Render can expose a standard
# PostgreSQL URI (postgres://user:password@host/db), while Spring's datasource
# uses a JDBC URL and receives credentials separately through DB_USERNAME and
# DB_PASSWORD. Strip URI userinfo before handing the URL to the JDBC driver.
case "${DATABASE_URL:-}" in
  postgres://*|postgresql://*)
    database_url="${DATABASE_URL#*://}"
    case "$database_url" in
      *@*) database_url="${database_url#*@}" ;;
    esac
    export DATABASE_URL="jdbc:postgresql://${database_url}"
    ;;
  jdbc:postgres://*|jdbc:postgresql://*)
    database_url="${DATABASE_URL#jdbc:postgres://}"
    database_url="${database_url#jdbc:postgresql://}"
    case "$database_url" in
      *@*) database_url="${database_url#*@}" ;;
    esac
    export DATABASE_URL="jdbc:postgresql://${database_url}"
    ;;
esac

exec java -jar /app/app.jar
