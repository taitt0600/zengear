#!/bin/sh
# One-off set-up of the acceptance database (docs/ACCEPTANCE.md), in the deploy order of docs/ARCHITECTURE.md:
# database, migrations, roles, seed. Re-runnable: an existing database is kept, the migration bundle applies only
# the pending migrations and every seed file is re-runnable (api/seed/README.md).
#
# The migrations build Phase 1 table by table, so zengear_08_roles.sql and a seed file can name a table or
# function the migrations have not built yet. Such a file is skipped as a whole (it runs in one transaction)
# when the object it misses is in the specification (api/db); any other error stops the set-up.
set -eu
export PGOPTIONS="${PGOPTIONS:--c client_min_messages=warning}"

DB=zengear
INIT=${INIT_DIR:-/init}
SPEC=$INIT/db

run_sql() {  # run_sql <database> <file>: 0 = applied, 3 = skipped (not migrated yet), exit on any other error
  if output=$(psql -X -q -1 -v ON_ERROR_STOP=1 -d "$1" -f "$2" 2>&1); then
    return 0
  fi
  missing=$(printf '%s\n' "$output" | sed -n 's/.*ERROR: *\(relation\|function\) "\{0,1\}\([a-z_]*\)"\{0,1\}.* does not exist.*/\2/p' | head -n 1)
  if [ -n "$missing" ] && grep -qiE "CREATE (TABLE|(OR REPLACE )?FUNCTION) $missing\b" "$SPEC"/zengear_0[2-5]_*.sql; then
    echo "init: skipped $(basename "$2"): $missing is not migrated yet"
    return 3
  fi
  printf '%s\n' "$output" >&2
  echo "init: $(basename "$2") failed" >&2
  exit 1
}

until pg_isready -q -d postgres; do sleep 1; done

if [ -z "$(psql -X -At -d postgres -c "SELECT 1 FROM pg_database WHERE datname = '$DB'")" ]; then
  echo "init: creating database $DB"
  psql -X -q -v ON_ERROR_STOP=1 -d postgres -f "$SPEC/zengear_01_database.sql"
fi

echo "init: applying migrations"
"$INIT/efbundle" --connection "Host=$PGHOST;Port=${PGPORT:-5432};Database=$DB;Username=$PGUSER;Password=$PGPASSWORD"

run_sql "$DB" "$SPEC/zengear_08_roles.sql" && echo "init: applied zengear_08_roles.sql" || true
for file in "$INIT"/seed/0*.sql; do
  run_sql "$DB" "$file" && echo "init: applied $(basename "$file")" || true
done
echo "init: done"
