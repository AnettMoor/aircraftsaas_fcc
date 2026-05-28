#!/bin/bash
set -e

# This script runs automatically when the PostgreSQL container is first initialized.
# It creates the additional module databases (fleet, booking).
# The first database (aircraft-users) is created automatically via POSTGRES_DB env var.

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    CREATE DATABASE "aircraft-fleet";
    CREATE DATABASE "aircraft-booking";
EOSQL
