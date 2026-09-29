-- ZenGear seed — the single business location of Phase 1 (LOC-01, R-LOC-01)
-- Run after the schema migrations, as the schema owner. Re-runnable: existing rows are kept.

-- Fill in the real store data, then remove the comment markers. Nothing is inserted until then.
-- INSERT INTO business_locations (name, phone, email, address_detail, province_id, ward_id, regular_hours)
-- SELECT '<tên cửa hàng>', '<hotline>', '<email>', '<số nhà, đường>', w.province_id, w.id,
--        '[{"day": 1, "open": "08:00", "close": "21:00"}]'::jsonb
-- FROM wards w WHERE w.code = '<mã phường/xã theo Quyết định 19/2025/QĐ-TTg>'
-- AND NOT EXISTS (SELECT 1 FROM business_locations);
