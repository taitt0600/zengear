-- Dev-only fixture of LOC-01, run by tools/dev_reset.ps1 after the seed and bootstrap-admin, never in production.
-- The business location is created once per environment at go-live (api/seed/08_business_location.sql, a template
-- filled with the real store) and no endpoint can add one (R-LOC-01); countries, provinces and wards come from the
-- official list (Quyết định 19/2025/QĐ-TTg), which is not in the repository. So only SQL reaches this state:
--   1. Sample geography rows: Vietnam, TP. Hồ Chí Minh with two wards and Hà Nội with one ward (for the refusal of a
--      ward outside the province, UC-070 3a). Sample rows only: the codes are not checked against the official list,
--      and loading that list later keeps them (ON CONFLICT DO NOTHING).
--   2. The one business location, with a fixed id named businessLocationId in the dev and ci environments; skipped
--      when a location exists already (a filled-in seed 08), so there is never a second one (R-LOC-01).

INSERT INTO countries (id, country_code, country_code3, common_name) VALUES (238, 'VN', 'VNM', 'Vietnam')
ON CONFLICT DO NOTHING;

INSERT INTO provinces (id, name, code, type, country_id) VALUES
  (79, 'Thành phố Hồ Chí Minh', '79', 'Thành phố', 238),
  (1, 'Thành phố Hà Nội', '01', 'Thành phố', 238)
ON CONFLICT DO NOTHING;

INSERT INTO wards (id, name, code, type, province_id) VALUES
  (26734, 'Phường Sài Gòn', '26734', 'Phường', 79),
  (26743, 'Phường Bến Thành', '26743', 'Phường', 79),
  (70, 'Phường Hoàn Kiếm', '00070', 'Phường', 1)
ON CONFLICT DO NOTHING;

INSERT INTO business_locations (id, name, phone, email, address_detail, province_id, ward_id, regular_hours)
SELECT '0199a0f3-0000-7000-8000-000000000001', 'ZenGear (fixture)', '02838123456', 'cskh@zengear.test',
  '123 Lê Lợi', 79, 26734,
  '[{"day": 1, "open": "08:30", "close": "21:30"}, {"day": 2, "open": "08:30", "close": "21:30"},
    {"day": 3, "open": "08:30", "close": "21:30"}, {"day": 4, "open": "08:30", "close": "21:30"},
    {"day": 5, "open": "08:30", "close": "21:30"}, {"day": 6, "open": "08:30", "close": "21:30"},
    {"day": 7, "open": "09:00", "close": "18:00"}]'
WHERE NOT EXISTS (SELECT 1 FROM business_locations);
