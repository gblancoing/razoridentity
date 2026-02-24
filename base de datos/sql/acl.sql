-- DROP SCHEMA acl;

CREATE SCHEMA acl AUTHORIZATION acl_app_user;
-- acl.permissions definition

-- Drop table

-- DROP TABLE acl.permissions;

CREATE TABLE acl.permissions (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	code text NOT NULL,
	description text NULL,
	created_at timestamptz DEFAULT now() NOT NULL,
	CONSTRAINT permissions_code_key UNIQUE (code),
	CONSTRAINT permissions_pkey PRIMARY KEY (id)
);


-- acl.roles definition

-- Drop table

-- DROP TABLE acl.roles;

CREATE TABLE acl.roles (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	"name" text NOT NULL,
	created_at timestamptz DEFAULT now() NOT NULL,
	CONSTRAINT roles_name_key UNIQUE (name),
	CONSTRAINT roles_pkey PRIMARY KEY (id)
);


-- acl.users definition

-- Drop table

-- DROP TABLE acl.users;

CREATE TABLE acl.users (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	email text NOT NULL,
	password_hash text NOT NULL,
	display_name text NULL,
	is_active bool DEFAULT true NOT NULL,
	created_at timestamptz DEFAULT now() NOT NULL,
	updated_at timestamptz DEFAULT now() NOT NULL,
	CONSTRAINT users_email_key UNIQUE (email),
	CONSTRAINT users_pkey PRIMARY KEY (id)
);


-- acl.refresh_tokens definition

-- Drop table

-- DROP TABLE acl.refresh_tokens;

CREATE TABLE acl.refresh_tokens (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	user_id uuid NOT NULL,
	token_hash text NOT NULL,
	issued_at timestamptz DEFAULT now() NOT NULL,
	expires_at timestamptz NOT NULL,
	revoked_at timestamptz NULL,
	user_agent text NULL,
	ip_address text NULL,
	CONSTRAINT refresh_tokens_pkey PRIMARY KEY (id),
	CONSTRAINT refresh_tokens_token_hash_key UNIQUE (token_hash),
	CONSTRAINT refresh_tokens_user_id_fkey FOREIGN KEY (user_id) REFERENCES acl.users(id) ON DELETE CASCADE
);
CREATE INDEX ix_acl_refresh_tokens_expires ON acl.refresh_tokens USING btree (expires_at);
CREATE INDEX ix_acl_refresh_tokens_user ON acl.refresh_tokens USING btree (user_id);


-- acl.role_permissions definition

-- Drop table

-- DROP TABLE acl.role_permissions;

CREATE TABLE acl.role_permissions (
	role_id uuid NOT NULL,
	permission_id uuid NOT NULL,
	CONSTRAINT role_permissions_pkey PRIMARY KEY (role_id, permission_id),
	CONSTRAINT role_permissions_permission_id_fkey FOREIGN KEY (permission_id) REFERENCES acl.permissions(id) ON DELETE CASCADE,
	CONSTRAINT role_permissions_role_id_fkey FOREIGN KEY (role_id) REFERENCES acl.roles(id) ON DELETE CASCADE
);


-- acl.user_roles definition

-- Drop table

-- DROP TABLE acl.user_roles;

CREATE TABLE acl.user_roles (
	user_id uuid NOT NULL,
	role_id uuid NOT NULL,
	CONSTRAINT user_roles_pkey PRIMARY KEY (user_id, role_id),
	CONSTRAINT user_roles_role_id_fkey FOREIGN KEY (role_id) REFERENCES acl.roles(id) ON DELETE CASCADE,
	CONSTRAINT user_roles_user_id_fkey FOREIGN KEY (user_id) REFERENCES acl.users(id) ON DELETE CASCADE
);


-- acl.user_tenant_scope definition

-- Drop table

-- DROP TABLE acl.user_tenant_scope;

CREATE TABLE acl.user_tenant_scope (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	user_id uuid NOT NULL,
	tenant_id uuid NOT NULL,
	partner_id uuid NULL,
	scope_type text DEFAULT 'tenant'::text NOT NULL,
	created_at timestamptz DEFAULT now() NOT NULL,
	CONSTRAINT user_tenant_scope_pkey PRIMARY KEY (id),
	CONSTRAINT user_tenant_scope_user_id_tenant_id_partner_id_scope_type_key UNIQUE (user_id, tenant_id, partner_id, scope_type),
	CONSTRAINT user_tenant_scope_user_id_fkey FOREIGN KEY (user_id) REFERENCES acl.users(id) ON DELETE CASCADE
);
CREATE INDEX ix_acl_user_tenant_scope_tenant ON acl.user_tenant_scope USING btree (tenant_id);
CREATE INDEX ix_acl_user_tenant_scope_user ON acl.user_tenant_scope USING btree (user_id);