-- ZenGear — schema snapshot (run on database zengear with psql -At)
-- Prints one sorted line per schema object of schema public so two databases can be compared with diff:
-- the database built by EF Core migrations and the database built from the phase files in api/db.
-- The EF Core history table is left out.

WITH t AS (
  SELECT c.oid, c.relname, c.relkind, c.reloptions
  FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
  WHERE n.nspname = 'public' AND c.relkind IN ('r', 'p', 'v') AND c.relname <> '__EFMigrationsHistory'
)
SELECT line FROM (
  SELECT format('extension %s', extname) AS line FROM pg_extension WHERE extname <> 'plpgsql'
  UNION ALL
  SELECT format('relation %s kind=%s options=%s', relname, relkind, COALESCE(array_to_string(reloptions, ','), '')) FROM t
  UNION ALL
  SELECT format('column %s.%s %s%s%s%s', t.relname, a.attname, format_type(a.atttypid, a.atttypmod),
                CASE WHEN a.attnotnull THEN ' not null' ELSE '' END,
                CASE WHEN d.adbin IS NOT NULL THEN ' default ' || pg_get_expr(d.adbin, d.adrelid) ELSE '' END,
                CASE WHEN a.attidentity <> '' THEN ' identity ' || a.attidentity::text ELSE '' END)
  FROM t JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum > 0 AND NOT a.attisdropped
  LEFT JOIN pg_attrdef d ON d.adrelid = a.attrelid AND d.adnum = a.attnum
  UNION ALL
  SELECT format('constraint %s.%s %s', t.relname, co.conname, pg_get_constraintdef(co.oid))
  FROM t JOIN pg_constraint co ON co.conrelid = t.oid
  WHERE co.contype <> 'n'
  UNION ALL
  SELECT format('index %s', indexdef) FROM pg_indexes WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory'
  UNION ALL
  SELECT format('trigger %s', pg_get_triggerdef(tg.oid)) FROM pg_trigger tg JOIN t ON t.oid = tg.tgrelid WHERE NOT tg.tgisinternal
  UNION ALL
  SELECT format('view %s %s', t.relname, md5(pg_get_viewdef(t.oid))) FROM t WHERE t.relkind = 'v'
  UNION ALL
  SELECT format('function %s(%s) %s', p.proname, pg_get_function_identity_arguments(p.oid), md5(pg_get_functiondef(p.oid)))
  FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
  WHERE n.nspname = 'public' AND p.prokind = 'f'
    AND NOT EXISTS (SELECT 1 FROM pg_depend dp WHERE dp.objid = p.oid AND dp.deptype = 'e')
) s
ORDER BY line;
