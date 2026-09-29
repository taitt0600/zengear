-- 1. Create database (connect to database: postgres)
DROP DATABASE IF EXISTS zengear WITH (FORCE);
CREATE DATABASE zengear TEMPLATE template0 ENCODING 'UTF8' LOCALE_PROVIDER icu ICU_LOCALE 'vi-VN' LOCALE 'C.UTF-8';
ALTER DATABASE zengear SET timezone TO 'UTC';
