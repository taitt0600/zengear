-- ZenGear schema — Future (run after Phase 3)

BEGIN;

-- 4. Catalog

CREATE TABLE product_badges (
  id         uuid        NOT NULL DEFAULT uuidv7(),
  code       varchar(50) NOT NULL,
  name       varchar     NOT NULL,
  bg_color   varchar(20),
  text_color varchar(20),
  priority   integer     NOT NULL DEFAULT 0,
  is_active  boolean     NOT NULL DEFAULT true,
  created_by uuid,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_by uuid,
  updated_at timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT pk_product_badges PRIMARY KEY (id)
);
CREATE UNIQUE INDEX ix_product_badges_code ON product_badges (code);

CREATE TABLE product_badge_assignments (
  product_id uuid        NOT NULL,
  badge_id   uuid        NOT NULL,
  starts_at  timestamptz,
  ends_at    timestamptz,
  created_by uuid,
  created_at timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT pk_product_badge_assignments PRIMARY KEY (product_id, badge_id),
  CONSTRAINT fk_product_badge_assignments_products_product_id FOREIGN KEY (product_id) REFERENCES products (id) ON DELETE CASCADE,
  CONSTRAINT fk_product_badge_assignments_product_badges_badge_id FOREIGN KEY (badge_id) REFERENCES product_badges (id) ON DELETE CASCADE,
  CONSTRAINT ck_product_badge_assignments_starts_at CHECK (starts_at is null or ends_at is null or ends_at > starts_at)
);
CREATE INDEX ix_product_badge_assignments_badge_id ON product_badge_assignments (badge_id);

CREATE TABLE product_tags (
  id          uuid        NOT NULL DEFAULT uuidv7(),
  title       varchar     NOT NULL,
  content     text        NOT NULL,
  type        varchar(20) NOT NULL DEFAULT 'PROMOTION',
  is_active   boolean     NOT NULL DEFAULT true,
  created_by  uuid,
  created_at  timestamptz NOT NULL DEFAULT now(),
  updated_by  uuid,
  updated_at  timestamptz NOT NULL DEFAULT now(),
  category_id uuid,
  product_id  uuid,
  CONSTRAINT pk_product_tags PRIMARY KEY (id),
  CONSTRAINT fk_product_tags_categories_category_id FOREIGN KEY (category_id) REFERENCES categories (id) ON DELETE CASCADE,
  CONSTRAINT fk_product_tags_products_product_id FOREIGN KEY (product_id) REFERENCES products (id) ON DELETE CASCADE,
  CONSTRAINT ck_product_tags_single_target CHECK ((category_id is not null)::int + (product_id is not null)::int = 1)
);
CREATE INDEX ix_product_tags_category_id ON product_tags (category_id);
CREATE INDEX ix_product_tags_product_id ON product_tags (product_id);

-- 6. Cart & Promotion

CREATE TABLE wishlists (
  user_id    uuid        NOT NULL,
  product_id uuid        NOT NULL,
  created_at timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT pk_wishlists PRIMARY KEY (user_id, product_id),
  CONSTRAINT fk_wishlists_users_user_id FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE,
  CONSTRAINT fk_wishlists_products_product_id FOREIGN KEY (product_id) REFERENCES products (id) ON DELETE CASCADE
);
CREATE INDEX ix_wishlists_product_id ON wishlists (product_id);

-- Columns added to tables of earlier phases
ALTER TABLE categories ADD COLUMN mega_menu_config jsonb;

COMMIT;
